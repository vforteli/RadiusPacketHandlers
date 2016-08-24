using Ais.Net.Radius;
using Ais.Net.Radius.Attributes;
using FlexinetsDBEF;
using log4net;
using System;
using System.Linq;
using System.Text;

namespace Flexinets.Radius
{
    public class RadiusDisconnector
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(RadiusDisconnector));
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly String _disconnectSecret;


        public RadiusDisconnector(FlexinetsEntitiesFactory contextFactory, String disconnectSecret)
        {
            _contextFactory = contextFactory;
            _disconnectSecret = disconnectSecret;
        }


        /// <summary>
        /// Check if a user should be disconnected based on acctsessionid
        /// Returns true if user should be disconnected
        /// </summary>
        /// <param name="acctSessionId"></param>
        /// <returns></returns>
        public Boolean CheckDisconnect(String acctSessionId)
        {
            _log.Info($"Checking disconnect for acctsessionid: {acctSessionId}");
            using (var db = _contextFactory.GetContext())
            {
                var status = db.CheckDisconnect(acctSessionId).SingleOrDefault();
                _log.Debug($"Check disconnect result for {acctSessionId}: {!status.HasValue}");
                return !status.HasValue;
            }
        }


        /// <summary>
        /// Send packet of disconnet
        /// </summary>
        /// <param name="remoteIpAddress"></param>
        /// <param name="port"></param>
        /// <param name="secret"></param>
        /// <param name="acctSessionId"></param>
        /// <param name="username">not used but needed...</param>
        public void SendPoD(String remoteIpAddress, UInt16 port, String acctSessionId)
        {
            try
            {
                _log.Info($"Sending pod for AcctSessionId {acctSessionId} to server: {remoteIpAddress}:{port}");

                var request = new Request(PacketType.Disconnect);
                request.Packet.Attributes.Add(new StringAttribute(AttributeType.AcctSessionId, acctSessionId));
                request.Packet.Secret = _disconnectSecret;

                var client = new Client(remoteIpAddress, 1700, _disconnectSecret);
                client.Ttl = 30;
                client.SendTimeout = 5000;
                client.ReceiveTimeout = 5000;
                var response = client.Send(request, false);
                if (response.Packet.Attributes.Count > 0)
                {
                    var hurr = Encoding.Default.GetString(response.Packet.Attributes[0].ValueArray);
                    _log.Fatal(remoteIpAddress + ": " + hurr);
                }
                else
                {
                    _log.Info($"{remoteIpAddress} probably disconnected {acctSessionId}");
                }
            }
            catch (Exception ex)
            {
                _log.Fatal("Something went haywire on disconnect. Start panicking!", ex);
            }
        }
    }
}