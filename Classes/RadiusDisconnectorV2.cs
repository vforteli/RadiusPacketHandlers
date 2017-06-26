using Flexinets.Radius.PacketHandlers.m2msimplify;
using FlexinetsDBEF;
using log4net;
using System;
using System.Linq;

namespace Flexinets.Radius
{
    public class RadiusDisconnectorV2
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(RadiusDisconnectorV2));
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly String _apiUsername;
        private readonly String _apiPassword;
        private readonly String _apiUrl;


        public RadiusDisconnectorV2(FlexinetsEntitiesFactory contextFactory, String apiUsername, String apiPassword, String apiUrl)
        {
            _contextFactory = contextFactory;
            _apiUsername = apiUsername;
            _apiPassword = apiPassword;
            _apiUrl = apiUrl;
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
        public Boolean DisconnectUserByMsisdn(String msisdn)
        {
            try
            {
                _log.Info($"Disconnecting msisdn {msisdn}");

                var client = new TAGExternalAPIImplService();
                client.Url = _apiUrl;
                var response = client.disconnectSessions(_apiUsername, _apiPassword, simIdentifier.MSISDN, new[] { msisdn });

                // todo verify resultcode
                Console.WriteLine(response.errorDescription);
                Console.WriteLine(response.resultCode);
                return true;
            }
            catch (Exception ex)
            {
                _log.Fatal($"Something went haywire disconnecting {msisdn}. Start panicking!", ex);
            }
            return false;
        }
    }
}