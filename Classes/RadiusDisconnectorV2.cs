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
        /// Disconnect a user by msisdn
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        public (Boolean success, String resultCode, String errorDescription) DisconnectUserByMsisdn(String msisdn)
        {
            try
            {
                _log.Info($"Disconnecting msisdn {msisdn}");
                var client = new TAGExternalAPIImplService()
                {
                    Url = _apiUrl
                };
                var response = client.disconnectSessions(_apiUsername, _apiPassword, simIdentifier.MSISDN, new[] { msisdn });
                return (response.resultCode == "OK", response.resultCode, response.errorDescription);
            }
            catch (Exception ex)
            {
                _log.Fatal($"Something went haywire disconnecting {msisdn}. Start panicking!", ex);
                return (false, "fail", ex.Message);
            }
        }
    }
}