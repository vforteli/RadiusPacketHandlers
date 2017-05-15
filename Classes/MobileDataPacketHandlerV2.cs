﻿using FlexinetsDBEF;
using log4net;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class MobileDataPacketHandlerV2 : IPacketHandler
    {
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly ILog _log = LogManager.GetLogger(typeof(MobileDataPacketHandlerV2));
        private readonly RadiusDisconnector _disconnector;
        private readonly WelcomeSender _welcomeSender;
        

        public MobileDataPacketHandlerV2(FlexinetsEntitiesFactory contextFactory, WelcomeSender welcomeSender, RadiusDisconnector disconnector)
        {
            _disconnector = disconnector;
            _welcomeSender = welcomeSender;
            _contextFactory = contextFactory;
        }


        public IRadiusPacket HandlePacket(IRadiusPacket packet)
        {
            if (packet.Code == PacketCode.AccessRequest)
            {
                return Authenticate(packet);
            }
            else if (packet.Code == PacketCode.AccountingRequest && packet.GetAttribute<AcctStatusTypes>("Acct-Status-Type") == AcctStatusTypes.Start)
            {
                return Start(packet);
            }
            else if (packet.Code == PacketCode.AccountingRequest && packet.GetAttribute<AcctStatusTypes>("Acct-Status-Type") == AcctStatusTypes.Stop)
            {
                return Stop(packet);
            }
            else if (packet.Code == PacketCode.AccountingRequest && packet.GetAttribute<AcctStatusTypes>("Acct-Status-Type") == AcctStatusTypes.InterimUpdate)
            {
                return Interim(packet);
            }            

            throw new InvalidOperationException($"Nothing configured for {packet.Code}");
        }


        private IRadiusPacket Authenticate(IRadiusPacket packet)
        {
            var msisdn = packet.GetAttribute<String>("Calling-Station-Id");
            var networkid = packet.GetAttribute<String>("3GPP-GGSN-MCC-MNC");

            _log.Debug($"Handling authentication packet for {msisdn} on network {msisdn}");
            using (var db = _contextFactory.GetContext())
            {               
                var result = db.Authenticate1(msisdn, "flexinets", msisdn, networkid).ToList();
                if (result.Count > 0 && result.First() == null)
                {
                    var response = packet.CreateResponsePacket(PacketCode.AccessAccept);
                    response.AddAttribute("Acct-Interim-Interval", 60);
                    return response;
                }
                else
                {
                    try
                    {
                        var mccmnc = Convert.ToInt32(networkid);
                        var network = db.Networks.SingleOrDefault(o => o.mccmnc == mccmnc);
                        var simcard = db.SimCards.SingleOrDefault(o => o.Msisdn == msisdn);

                        var sb = new StringBuilder();

                        sb.AppendLine($"Authentication failed for {msisdn} on network {mccmnc} ({network.providername}, {network.countryname})");
                        if (simcard.user_id == null)
                        {
                            sb.AppendLine("Sim card not mapped to a user");
                        }
                        else
                        {
                            sb.AppendLine($"User: {simcard.UserSetting.user.username}@{simcard.UserSetting.user.realm}, group: {simcard.UserSetting.user.directory.name}");
                        }
                        _log.Info(sb.ToString().Trim()); // todo needs throttling to reduce unwanted spam
                    }
                    catch (Exception ex)
                    {
                        _log.Error("huh?", ex);
                    }

                    return packet.CreateResponsePacket(PacketCode.AccessReject);
                }
            }
        }


        private IRadiusPacket Start(IRadiusPacket packet)
        {
            throw new NotImplementedException();
            /*
            var user = UsernameDomain.Parse(packet.GetAttribute<String>("User-Name"));
            var msisdn = packet.GetAttribute<String>("Calling-Station-Id");
            var acctSessionId = packet.GetAttribute<String>("Acct-Session-Id");
            var acctStatusType = "Start";    // duuh

            _log.Debug($"Handling start packet for {msisdn} with AcctSessionId {acctSessionId}");
            using (var db = _contextFactory.GetContext())
            {
                db.AccountingStart(user.Username, user.Domain, msisdn, acctStatusType, acctSessionId, null);
            }

            Task.Run(() => _welcomeSender.CheckWelcomeSms(msisdn));
            return packet.CreateResponsePacket(PacketCode.AccountingResponse);
            */
        }


        private IRadiusPacket Interim(IRadiusPacket packet)
        {
            throw new NotImplementedException();
            /*
            var user = UsernameDomain.Parse(packet.GetAttribute<String>("User-Name"));
            var msisdn = packet.GetAttribute<String>("Calling-Station-Id");
            var acctSessionId = packet.GetAttribute<String>("Acct-Session-Id");
            var acctStatusType = "Alive";    // duuh
            var acctInputOctets = packet.GetAttribute<UInt32>("Acct-Input-Octets");
            var acctOutputOctets = packet.GetAttribute<UInt32>("Acct-Output-Octets");
            var acctSessionTime = packet.GetAttribute<UInt32>("Acct-Session-Time");
            var acctInputGigawords = packet.GetAttribute<UInt32?>("Acct-Input-Gigawords");
            var acctOutputGigawords = packet.GetAttribute<UInt32?>("Acct-Output-Gigawords");
            var nasIpAddress = packet.GetAttribute<IPAddress>("NAS-IP-Address");

            _log.Debug($"Handling interim packet for {msisdn} with AcctSessionId {acctSessionId}");
            using (var db = _contextFactory.GetContext())
            {
                db.AccountingInterim(user.Username, user.Domain, msisdn, acctStatusType, acctSessionId, acctInputOctets, acctOutputOctets,
                    (int)acctSessionTime, acctInputGigawords, acctOutputGigawords);
            }



            Task.Run(() =>
            {
                if (_disconnector.CheckDisconnect(acctSessionId))
                {
                    _disconnector.SendPoD(nasIpAddress.ToString(), 1700, acctSessionId);
                }
            });

            return packet.CreateResponsePacket(PacketCode.AccountingResponse);
            */
        }


        private IRadiusPacket Stop(IRadiusPacket packet)
        {
            throw new NotImplementedException();
            /*
            var user = UsernameDomain.Parse(packet.GetAttribute<String>("User-Name"));
            var msisdn = packet.GetAttribute<String>("Calling-Station-Id");
            var acctSessionId = packet.GetAttribute<String>("Acct-Session-Id");
            var acctStatusType = "Stop";    // duuh
            var acctInputOctets = packet.GetAttribute<UInt32>("Acct-Input-Octets");
            var acctOutputOctets = packet.GetAttribute<UInt32>("Acct-Output-Octets");
            var acctSessionTime = packet.GetAttribute<UInt32>("Acct-Session-Time");
            var acctTerminateCause = packet.GetAttribute<UInt32>("Acct-Terminate-Cause");   // oh crap, guess i need values as well...
            var acctInputGigawords = packet.GetAttribute<UInt32?>("Acct-Input-Gigawords");
            var acctOutputGigawords = packet.GetAttribute<UInt32?>("Acct-Output-Gigawords");

            _log.Debug($"Handling stop packet for {msisdn} with AcctSessionId {acctSessionId}");
            using (var db = _contextFactory.GetContext())
            {
                db.AccountingStop(user.Username, user.Domain, msisdn, acctStatusType, acctSessionId, acctInputOctets, acctOutputOctets,
                    (int)acctSessionTime, acctTerminateCause.ToString(), acctInputGigawords, acctOutputGigawords);
            }

            return packet.CreateResponsePacket(PacketCode.AccountingResponse);
            */
        }


        public void Dispose()
        {

        }
    }
}