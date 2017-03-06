using Flexinets.Radius.PacketHandlers;
using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;

namespace Flexinets.Radius
{
    public class NetworkIdProvider
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(NetworkIdProvider));
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly String _apiUrl;
        private readonly IWebClientFactory _webClientFactory;
        private readonly NetworkProvider _networkProvider;

        private readonly ConcurrentDictionary<String, CacheEntry> _networkIdCache = new ConcurrentDictionary<String, CacheEntry>();
        private readonly ConcurrentDictionary<String, Task<String>> _pendingApiRequests = new ConcurrentDictionary<String, Task<String>>();
        private readonly ConcurrentDictionary<String, FailedRequestBackOffCounter> _backOffCounter = new ConcurrentDictionary<String, FailedRequestBackOffCounter>();

        private NetworkCredential _apiCredential;

        private readonly Int32 cacheTimeout = 30;        


        /// <summary>
        /// Provider for getting the network id from FL1
        /// </summary>
        /// <param name="contextFactory"></param>
        public NetworkIdProvider(FlexinetsEntitiesFactory contextFactory, String apiUrl, IDateTimeProvider dateTimeProvider, IWebClientFactory webClientFactory, NetworkProvider networkProvider)
        {
            _apiUrl = apiUrl;
            _contextFactory = contextFactory;
            _apiCredential = GetApiCredentials();
            _dateTimeProvider = dateTimeProvider;
            _webClientFactory = webClientFactory;
            _networkProvider = networkProvider;
        }


        public Boolean TryGetNetworkId(String msisdn, out String networkId)
        {
            networkId = null;

            FailedRequestBackOffCounter counter;
            if (_backOffCounter.TryGetValue(msisdn, out counter))
            {
                if (_dateTimeProvider.UtcNow < counter.NextAttempt)
                {
                    _log.Warn($"Waiting until {counter.NextAttempt} for msisdn {msisdn}. {counter.FailureCount} consecutive failed networkid api requests");
                    return false;
                }
            }

            try
            {
                networkId = GetNetworkId(msisdn);
                _backOffCounter.TryRemove(msisdn, out counter);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Could not get networkid for {msisdn}", ex);
                counter = new FailedRequestBackOffCounter(1, _dateTimeProvider.UtcNow);
                _backOffCounter.AddOrUpdate(msisdn, counter, (key, value) => new FailedRequestBackOffCounter(value.FailureCount + 1, _dateTimeProvider.UtcNow));
                return false;
            }
        }


        /// <summary>
        /// Get the mccmnc for a msisdn
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        public String GetNetworkId(String msisdn)
        {
            String networkId = null;

            CacheEntry cacheEntry;
            _log.Debug($"Getting network id for msisdn {msisdn}");
            if (_networkIdCache.TryGetValue(msisdn, out cacheEntry))
            {
                _log.Debug($"Found cache entry {cacheEntry.NetworkId} for msisdn {msisdn}");
                if (_dateTimeProvider.UtcNow.Subtract(cacheEntry.DateSet).TotalSeconds < cacheTimeout)
                {
                    _log.Debug($"Cache entry less than {cacheTimeout} seconds old!");
                    networkId = cacheEntry.NetworkId;
                }
            }
            if (networkId == null)
            {
                networkId = GetId(msisdn);
            }

            var entry = new CacheEntry { NetworkId = networkId, DateSet = _dateTimeProvider.UtcNow };
            _networkIdCache.AddOrUpdate(msisdn, entry, (s, i) => entry);

            _log.Debug($"Refreshed cache entry for msisdn {msisdn}, networkid {networkId}");
            return networkId;
        }


        private String GetId(String msisdn)
        {
            try
            {
                return GetNetworkIdFromApi(msisdn).Result;
            }
            catch (WebException ex)
            {
                // If the password has been changed, refresh credentials and try again
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    _log.Warn("Got 401, refreshing API credentials from database and retrying", ex);
                    _apiCredential = GetApiCredentials();
                    return GetNetworkIdFromApi(msisdn).Result;
                }
                throw;
            }
        }


        /// <summary>
        /// GetNetworkIdFromApi
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        internal async Task<String> GetNetworkIdFromApi(String msisdn)
        {
            Task<String> task;
            try
            {
                if (!_pendingApiRequests.TryGetValue(msisdn, out task))
                {
                    _log.Debug($"Starting new api request for msisdn {msisdn}");
                    var client = _webClientFactory.Create();
                    client.Credentials = _apiCredential;
                    task = client.DownloadStringTaskAsync(_apiUrl + msisdn);
                    _pendingApiRequests.TryAdd(msisdn, task);
                }
                else
                {
                    _log.Debug($"Waiting for previous api request for msisdn {msisdn}");
                }
                var response = await task;
                return ParseApiResponseXml(response, msisdn);
            }
            finally
            {
                _pendingApiRequests.TryRemove(msisdn, out task);
            }
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