using Flexinets.Security;
using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Flexinets.Radius
{
    public class iPassPacketHandler : IPacketHandler
    {
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly ILog _log = LogManager.GetLogger(typeof(iPassPacketHandler));
        private readonly HashSet<String> _failures = new HashSet<String>();


        public iPassPacketHandler(FlexinetsEntitiesFactory contextFactory)
        {
            _contextFactory = contextFactory;
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
            var acctStatusType = packet.GetAttribute<AcctStatusTypes>("Acct-Status-Type");
            if (acctStatusType == AcctStatusTypes.Start || acctStatusType == AcctStatusTypes.Stop)
            {
                var user = GetUser(packet.GetAttribute<String>("User-Name"));
                _log.Info($"Handling {acctStatusType} packet for {packet.GetAttribute<String>("User-Name")}");
                try
                {
                    using (var db = _contextFactory.GetContext())
                    {
                        var entry = new radiatoraccounting
                        {
                            ACCTSTATUSTYPE = (packet.GetAttribute<AcctStatusTypes>("Acct-Status-Type")).ToString(),
                            ACCTINPUTOCTETS = Convert.ToUInt32(packet.GetAttribute<UInt32?>("Acct-Input-Octets")),
                            ACCTOUTPUTOCTETS = Convert.ToUInt32(packet.GetAttribute<UInt32?>("Acct-Output-Octets")),
                            ACCTSESSIONID = packet.GetAttribute<String>("Acct-Session-Id"),
                            ACCTSESSIONTIME = Convert.ToInt32(packet.GetAttribute<UInt32?>("Acct-Session-Time")),
                            NASIDENTIFIER = packet.GetAttribute<String>("NAS-Identifier"),
                            NASPORT = packet.GetAttribute<UInt32?>("NAS-Port"),
                            NASPORTTYPE = packet.GetAttribute<UInt32?>("NAS-Port-Type").ToString(),
                            WISPrLocationName = packet.GetAttribute<String>("WISPr-Location-Name"),
                            temp = packet.GetAttribute<String>("Ipass-Location-Description"),
                            username = user.username,
                            realm = user.realm,
                            node_id = user.node_id,
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

                if (acctStatusType == AcctStatusTypes.Start)
                {
                    try
                    {
                        using (var db = _contextFactory.GetContext())
                        {
                            db.radiatoronlines.Add(new radiatoronline
                            {
                                username = user.username,
                                realm = user.realm,
                                ACCTSESSIONID = packet.GetAttribute<String>("Acct-Session-Id"),
                                timestamp_datetime = packet.Attributes.ContainsKey("Timestamp") ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(packet.GetAttribute<Int32>("Timestamp")).UtcDateTime : DateTime.UtcNow,
                                NASIDENTIFIER = packet.GetAttribute<String>("NAS-Identifier"),
                                NASPORT = packet.GetAttribute<UInt32?>("NAS-Port"),
                                NASPORTTYPE = packet.GetAttribute<UInt32?>("NAS-Port-Type").ToString(),
                                node_id = user.node_id,
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
                if (acctStatusType == AcctStatusTypes.Stop)
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

            // Separate handler for tb?        
            if (usernamedomain.EndsWith("@tb.flexinets.se"))
            {
                _log.Info("Forwarding tb authentication");
                return packet.CreateResponsePacket(ProxyAuthentication(usernamedomain, packetPassword));
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
                        var username = Utils.SplitUsernameDomain(usernamedomain);
                        var user = db.users.SingleOrDefault(o => o.username == username.Username && o.realm == username.Domain);
                        if (user == null)
                        {
                            _log.Warn($"Username {usernamedomain} not found");
                        }
                        else if (user.status != 1)
                        {
                            _log.Warn($"Username {usernamedomain} is not active, email: {user.email}");
                            var location = packet.GetAttribute<String>("Ipass-Location-Description");
                            if (!String.IsNullOrEmpty(location))
                            {
                                _log.Warn($"iPass location description: {location}");
                            }
                        }
                        else
                        {
                            _log.Warn($"Bad password for user {usernamedomain}, password is {packetPassword.Length} characters, email: {user.email}");
                            var location = packet.GetAttribute<String>("Ipass-Location-Description"); 
                            if (!String.IsNullOrEmpty(location))
                            {
                                _log.Warn($"iPass location description: {location}");
                            }
                        }

                        _failures.Add(usernamedomain);

                        return packet.CreateResponsePacket(PacketCode.AccessReject);
                    }
                }
            }
        }


        /// <summary>
        /// Get a user from db
        /// </summary>
        /// <param name="rawusername"></param>
        /// <returns></returns>
        private user GetUser(String rawusername)
        {
            var user = Utils.SplitUsernameDomain(rawusername);
            using (var db = _contextFactory.GetContext())
            {
                return db.users.SingleOrDefault(o => o.username == user.Username && o.realm == user.Domain);
            }
        }


        /// <summary>
        /// Proxy authentication to another roamserver using checkipass
        /// </summary>
        /// <param name="usernamedomain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private PacketCode ProxyAuthentication(String usernamedomain, String password)
        {
            // todo add indefinite password caching?
            // todo these should be injected
            var host = "127.0.0.1";
            var checkIpassPath = @"c:\ipass\roamserver\6.1.0\test\checkipass.bat";

            var path = $"/C {checkIpassPath} -u {usernamedomain} -p {password} -host {host} -type auth";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", path)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var sb = new StringBuilder();
            process.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.StandardInput.WriteLine();  // Exits the script
            process.WaitForExit();

            var content = sb.ToString();
            _log.Debug(content);

            if (content.Contains("Status: accept"))
            {
                return PacketCode.AccessAccept;
            }
            return PacketCode.AccessReject;
        }


        public void Dispose()
        {

        }
    }
}