//using FlexinetsDBEF;
//using log4net;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Flexinets.Radius
//{
//    public class RadiusDisconnector
//    {
//        private static readonly ILog _log = LogManager.GetLogger(typeof(RadiusDisconnector));
//        private readonly FlexinetsEntitiesFactory _contextFactory;
//        private readonly String _secret;
//        private readonly String _servers;


//        public RadiusDisconnector(FlexinetsEntitiesFactory contextFactory, String radiusServers, String secret)
//        {
//            _contextFactory = contextFactory;
//            _secret = secret;
//            _servers = radiusServers;
//        }


//        /// <summary>
//        /// Check if a user should be disconnected based on acctsessionid
//        /// Returns true if user should be disconnected
//        /// </summary>
//        /// <param name="acctSessionId"></param>
//        /// <returns></returns>
//        public Boolean CheckDisconnect(String acctSessionId)
//        {
//            _log.Info($"Checking disconnect for acctsessionid: {acctSessionId}");
//            using (var db = _contextFactory.GetContext())
//            {
//                var status = db.CheckDisconnect(acctSessionId).SingleOrDefault();
//                _log.Debug($"Check disconnect result for {acctSessionId}: {!status.HasValue}");
//                return !status.HasValue;
//            }
//        }


//        /// <summary>
//        /// Sends a PoD to disconnect a user
//        /// </summary>
//        /// <param name="usernameDomain"></param>
//        /// <param name="acctSessionId"></param>
//        public void DisconnectUser(String acctSessionId)
//        {
//            _log.Info($"Sending PoD for AcctSessionId: {acctSessionId}");

//            var servers = _servers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
//            Parallel.ForEach(servers, server => SendPoD(server, 1700, _secret, acctSessionId));
//        }


//        /// <summary>
//        /// Send a packet of disconnect
//        /// </summary>
//        /// <param name="ipaddress"></param>
//        /// <param name="port"></param>
//        /// <param name="radiussecret"></param>
//        /// <param name="acctSessionId"></param>
//        public void SendPoD(String ipaddress, Int16 port, String radiussecret, String acctSessionId)
//        {
//            try
//            {
//                _log.Info($"Sending pod to server: {ipaddress}:{port}");

//                var acctsessionid = new StringAttribute(AttributeType.AcctSessionId, acctSessionId);
//                var request = new Request(PacketType.Disconnect, "127.0.0.1", ServiceType.Unknown, null);
//                request.Packet.Attributes.Add(acctsessionid);
//                request.Packet.Secret = radiussecret;

//                var client = new Client(ipaddress, 1700, radiussecret);
//                var response = client.Send(request, false);
//                if (response.Packet.Attributes.Count > 0)
//                {
//                    var hurr = Encoding.Default.GetString(response.Packet.Attributes[0].ValueArray);
//                    _log.Info(ipaddress + ": " + hurr);
//                }
//                else
//                {
//                    _log.Info($"{ipaddress}: probably disconnected {acctSessionId}");
//                }
//            }
//            catch (Exception ex)
//            {
//                _log.Error(ex.ToString());
//            }
//        }
//    }
//}
