using Flexinets.Radius.PacketHandlers;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class NetworkIdProvider
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(NetworkIdProvider));
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly NetworkApiClient _networkApiClient;

        private readonly ConcurrentDictionary<String, CacheEntry> _networkIdCache = new ConcurrentDictionary<String, CacheEntry>();
        private readonly ConcurrentDictionary<String, Task<String>> _pendingApiRequests = new ConcurrentDictionary<String, Task<String>>();
        private readonly ConcurrentDictionary<String, FailedRequestBackOffCounter> _backOffCounter = new ConcurrentDictionary<String, FailedRequestBackOffCounter>();

        private readonly Int32 cacheTimeout = 30;


        /// <summary>
        /// Provider for getting the network id from FL1
        /// </summary>
        /// <param name="contextFactory"></param>
        public NetworkIdProvider(IDateTimeProvider dateTimeProvider, NetworkApiClient networkApiClient)
        {
            _dateTimeProvider = dateTimeProvider;
            _networkApiClient = networkApiClient;
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

            _log.Debug($"Getting network id for msisdn {msisdn}");
            if (_networkIdCache.TryGetValue(msisdn, out CacheEntry cacheEntry))
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
                networkId = GetNetworkIdFromApi(msisdn).Result;
            }

            var entry = new CacheEntry { NetworkId = networkId, DateSet = _dateTimeProvider.UtcNow };
            _networkIdCache.AddOrUpdate(msisdn, entry, (s, i) => entry);

            _log.Debug($"Refreshed cache entry for msisdn {msisdn}, networkid {networkId}");
            return networkId;
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
                    task = _networkApiClient.GetId(msisdn);
                    _pendingApiRequests.TryAdd(msisdn, task);
                }
                else
                {
                    _log.Debug($"Waiting for previous api request for msisdn {msisdn}");
                }
                return await task;
            }
            finally
            {
                _pendingApiRequests.TryRemove(msisdn, out task);
            }
        }
    }
}