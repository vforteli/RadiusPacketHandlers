using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;

namespace Flexinets.Radius.PacketHandlers
{
    public class NetworkApiClient
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(NetworkApiClient));
        private readonly IWebClientFactory _webClientFactory;
        private readonly NetworkProvider _networkProvider;
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly String _apiUrl;
        private readonly ConcurrentDictionary<String, Task<String>> _pendingApiRequests = new ConcurrentDictionary<String, Task<String>>();
        public NetworkCredential ApiCredential
        {
            get; set;
        }

        public NetworkApiClient(FlexinetsEntitiesFactory contextFactory, IWebClientFactory webClientFactory, NetworkProvider networkProvider, String apiUrl)
        {
            _contextFactory = contextFactory;
            _webClientFactory = webClientFactory;
            _networkProvider = networkProvider;
            _apiUrl = apiUrl;
            ApiCredential = GetApiCredentials();
        }


        internal async Task<String> GetId(String msisdn)
        {
            try
            {
                return await GetNetworkIdFromApi(msisdn);
            }
            catch (WebException ex)
            {
                // If the password has been changed, refresh credentials and try again
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    _log.Warn("Got 401, refreshing API credentials from database and retrying");
                    ApiCredential = GetApiCredentials();
                    return await GetNetworkIdFromApi(msisdn);
                }
                throw;
            }
        }


        /// <summary>
        /// GetNetworkIdFromApi
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        private async Task<String> GetNetworkIdFromApi(String msisdn)
        {
            var client = _webClientFactory.Create();
            client.Credentials = ApiCredential;
            var response = await client.DownloadStringTaskAsync(_apiUrl + msisdn);
            return ParseApiResponseXml(response, msisdn);
        }


        /// <summary>
        /// Parse the api response xml and try to find the network id
        /// </summary>
        /// <param name="response"></param>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        private String ParseApiResponseXml(String response, String msisdn)
        {
            var document = new XmlDocument();
            document.LoadXml(response);
            if (document.GetElementsByTagName("message")[0].InnerText == "ok")
            {
                var networkId = document.GetElementsByTagName("MCC_MNC")[0].InnerText;

                if (!_networkProvider.IsValidNetwork(networkId))
                {
                    //todo add logic for parsing VLR global title in case mccmnc lookup fails?
                    _log.Error($"No valid network id found for {msisdn}, VLR_address: {document.GetElementsByTagName("VLR_address")[0].InnerText}");
                    throw new InvalidOperationException($"Got invalid networkid {networkId} for msisdn {msisdn}");
                }

                return networkId;
            }

            _log.Error(document.ToReadableString());
            throw new InvalidOperationException("NetworkId Api failed, see logs for details");
        }


        /// <summary>
        /// Get the credentials for FL1 network id API
        /// </summary>
        /// <returns></returns>
        private NetworkCredential GetApiCredentials()
        {
            using (var db = _contextFactory.GetContext())
            {
                return new NetworkCredential
                {
                    UserName = db.FL1Settings.SingleOrDefault(o => o.Name == "ApiUsername").Value,
                    Password = db.FL1Settings.SingleOrDefault(o => o.Name == "ApiPassword").Value
                };
            }
        }
    }
}
