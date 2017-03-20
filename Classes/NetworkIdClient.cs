using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Data.Entity;
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

            try
            {
                ApiCredential = GetApiCredentialsAsync().Result;
            }
            catch (Exception ex)
            {
                _log.Error("Unable to get api credentials from db", ex);
            }
        }


        internal async Task<String> GetId(String msisdn)
        {
            try
            {
                if (ApiCredential == null)
                {
                    ApiCredential = await GetApiCredentialsAsync();
                }
                return await GetNetworkIdFromApi(msisdn, _apiUrl, ApiCredential);
            }
            catch (WebException ex)
            {
                // If the password has been changed, refresh credentials and try again
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    _log.Warn("Got 401, refreshing API credentials from database and retrying");
                    ApiCredential = await GetApiCredentialsAsync();
                    return await GetNetworkIdFromApi(msisdn, _apiUrl, ApiCredential);
                }
                throw;
            }
        }


        /// <summary>
        /// GetNetworkIdFromApi
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        private async Task<String> GetNetworkIdFromApi(String msisdn, String apiUrl, NetworkCredential apiCredential)
        {
            var client = _webClientFactory.Create(apiCredential);
            var response = await client.DownloadStringTaskAsync(apiUrl + msisdn);
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
        private async Task<NetworkCredential> GetApiCredentialsAsync()
        {
            using (var db = _contextFactory.GetContext())
            {
                return new NetworkCredential
                {
                    UserName = (await db.FL1Settings.SingleOrDefaultAsync(o => o.Name == "ApiUsername")).Value,
                    Password = (await db.FL1Settings.SingleOrDefaultAsync(o => o.Name == "ApiPassword")).Value
                };
            }
        }
    }
}
