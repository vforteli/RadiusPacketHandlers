using Flexinets.Radius.PacketHandlers;
using Flexinets.Security;
using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace Flexinets.Radius
{
    public class iPassPacketHandler : IPacketHandler
    {
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly ILog _log = LogManager.GetLogger(typeof(iPassPacketHandler));
        private readonly iPassAuthenticationProxy _authProxy;
        private readonly HashSet<String> _failures = new HashSet<String>();


        public iPassPacketHandler(FlexinetsEntitiesFactory contextFactory, iPassAuthenticationProxy authProxy)
        {
            _contextFactory = contextFactory;
            _authProxy = authProxy;
        }


        public IRadiusPacket HandlePacket(IRadiusPacket packet)
        {
            if (packet.Code == PacketCode.AccountingRequest)
            {
                return HandleAccountingPacket(packet);
            }
            else if (packet.Code == PacketCode.AccessRequest)
            {
                return HandleAuthenticationPacket(packet);
            }
            return null;
        }


        /// <summary>
        /// Accounting
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private IRadiusPacket HandleAccountingPacket(IRadiusPacket packet)
        {
            var acctStatusType = packet.GetAttribute<AcctStatusType>("Acct-Status-Type");
            if (acctStatusType == AcctStatusType.Start || acctStatusType == AcctStatusType.Stop)
            {
                var usernamedomain = UsernameDomain.Parse(packet.GetAttribute<String>("User-Name"));
                var nodeid = GetUserNodeId(usernamedomain.Username, usernamedomain.Domain);
                _log.Info($"Handling {acctStatusType} packet for {usernamedomain}");
                try
                {
                    using (var db = _contextFactory.GetContext())
                    {
                        var entry = new radiatoraccounting
                        {
                            username = usernamedomain.Username,
                            realm = usernamedomain.Domain,
                            node_id = nodeid,
                            ACCTSTATUSTYPE = (packet.GetAttribute<AcctStatusType>("Acct-Status-Type")).ToString(),
                            ACCTINPUTOCTETS = Convert.ToUInt32(packet.GetAttribute<UInt32?>("Acct-Input-Octets")),
                            ACCTOUTPUTOCTETS = Convert.ToUInt32(packet.GetAttribute<UInt32?>("Acct-Output-Octets")),
                            ACCTSESSIONID = packet.GetAttribute<String>("Acct-Session-Id"),
                            ACCTSESSIONTIME = Convert.ToInt32(packet.GetAttribute<UInt32?>("Acct-Session-Time")),
                            NASIDENTIFIER = packet.GetAttribute<String>("NAS-Identifier"),
                            NASPORT = packet.GetAttribute<UInt32?>("NAS-Port"),
                            NASPORTTYPE = packet.GetAttribute<UInt32?>("NAS-Port-Type").ToString(),
                            WISPrLocationName = packet.GetAttribute<String>("WISPr-Location-Name"),
                            temp = packet.GetAttribute<String>("Ipass-Location-Description"),                            
                            timestamp_datetime = packet.Attributes.ContainsKey("Timestamp") ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(packet.GetAttribute<Int32>("Timestamp")).UtcDateTime : DateTime.UtcNow
                        };
                        db.radiatoraccountings.Add(entry);
                        db.SaveChanges();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    _log.Info($"Duplicate {acctStatusType} request received");
                }
                catch (Exception ex)
                {
                    _log.Error("Something went wrong", ex);
                }

                if (acctStatusType == AcctStatusType.Start)
                {
                    try
                    {
                        using (var db = _contextFactory.GetContext())
                        {
                            db.radiatoronlines.Add(new radiatoronline
                            {
                                username = usernamedomain.Username,
                                realm = usernamedomain.Domain,
                                node_id = nodeid,
                                ACCTSESSIONID = packet.GetAttribute<String>("Acct-Session-Id"),
                                timestamp_datetime = packet.Attributes.ContainsKey("Timestamp") ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(packet.GetAttribute<Int32>("Timestamp")).UtcDateTime : DateTime.UtcNow,
                                NASIDENTIFIER = packet.GetAttribute<String>("NAS-Identifier"),
                                NASPORT = packet.GetAttribute<UInt32?>("NAS-Port"),
                                NASPORTTYPE = packet.GetAttribute<UInt32?>("NAS-Port-Type").ToString(),
                                WISPrLocationName = packet.GetAttribute<String>("Ipass-Location-Description")
                            });
                            db.SaveChanges();
                        }
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        _log.Info("Cannot insert duplicate in radiatoronline");
                    }
                }
                if (acctStatusType == AcctStatusType.Stop)
                {
                    try
                    {
                        using (var db = _contextFactory.GetContext())
                        {
                            var acctsessionid = packet.GetAttribute<String>("Acct-Session-Id");
                            var online = db.radiatoronlines.SingleOrDefault(o => o.ACCTSESSIONID == acctsessionid);
                            if (online != null)
                            {
                                db.radiatoronlines.Remove(online);
                                db.SaveChanges();
                            }
                        }
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        _log.Info("Nothing to remove from online");
                    }
                }
            }
            return packet.CreateResponsePacket(PacketCode.AccountingResponse);
        }


        /// <summary>
        /// Authentication
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private IRadiusPacket HandleAuthenticationPacket(IRadiusPacket packet)
        {
            var usernamedomain = packet.GetAttribute<String>("User-Name").ToLowerInvariant();
            var packetPassword = packet.GetAttribute<String>("User-Password");

            _log.Info($"Handling {packet.Code} packet for {packet.GetAttribute<String>("User-Name")}");

            var proxyresponse = _authProxy.ProxyAuthentication(usernamedomain, packetPassword);
            if (proxyresponse.HasValue)
            {
                _log.Info($"Got response from proxy for username {usernamedomain}");

                // todo refactor...
                if (proxyresponse == PacketCode.AccessReject)
                {
                    _failures.Add(usernamedomain);
                }
                else if (proxyresponse == PacketCode.AccessAccept)
                {
                    if (_failures.Contains(usernamedomain))
                    {
                        _log.Warn($"Username {usernamedomain} authenticated after failures");
                        _failures.Remove(usernamedomain);
                    }
                }
                return packet.CreateResponsePacket(proxyresponse.Value);
            }           
            else
            {
                using (var db = _contextFactory.GetContext())
                {
                    var passwordhash = db.Authenticate(usernamedomain, packetPassword).SingleOrDefault();
                    if (CryptoMethods.isValidPassword(passwordhash, packetPassword))
                    {
                        if (_failures.Contains(usernamedomain))
                        {
                            _log.Warn($"Username {usernamedomain} authenticated after failures");
                            _failures.Remove(usernamedomain);
                        }
                        return packet.CreateResponsePacket(PacketCode.AccessAccept);
                    }
                    else
                    {
                        // todo remove transition period stuff...
                        var username = UsernameDomain.Parse(usernamedomain);
                        var user = db.users.SingleOrDefault(o => o.username == username.Username && o.realm == username.Domain);
                        if (user == null)
                        {
                            _log.Warn($"Username {usernamedomain} not found");
                        }
                        else if (user.status != 1)
                        {
                            _log.Warn($"Username {usernamedomain} is not active, email: {user.email}");
                        }
                        else
                        {
                            _log.Warn($"Bad password for user {usernamedomain}, password is {packetPassword.Length} characters, email: {user.email}");                         
                        }

                        var location = packet.GetAttribute<String>("Ipass-Location-Description");
                        if (!String.IsNullOrEmpty(location))
                        {
                            _log.Warn($"iPass location description: {location}");
                        }

                        _failures.Add(usernamedomain);

                        return packet.CreateResponsePacket(PacketCode.AccessReject);
                    }
                }
            }
        }


        /// <summary>
        /// Get a node id for a user or domain
        /// </summary>
        /// <param name="rawusername"></param>
        /// <returns></returns>
        private Int32 GetUserNodeId(String username, String domain)
        {
            using (var db = _contextFactory.GetContext())
            {
                var user = db.users.SingleOrDefault(o => o.username == username && o.realm == domain);
                if (user != null)   // Fully qualified username
                {
                    return user.node_id;
                }
                else // Domain mapped
                {
                    var nodeid = from o in db.directories
                                 where o.ipass_realms.Any(r => r.realm == domain)
                                 where o.status == 1
                                 orderby o.node_id
                                 select o.node_id;

                    return nodeid.FirstOrDefault();
                }
            }
        }


        public void Dispose()
        {

        }
    }
}