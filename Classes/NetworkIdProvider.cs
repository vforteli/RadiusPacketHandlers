using Flexinets.Radius.PacketHandlers;
using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;

namespace Flexinets.Radius
{
    public class NetworkIdProvider
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(NetworkIdProvider));
        private readonly ConcurrentDictionary<String, CacheEntry> _networkIdCache = new ConcurrentDictionary<String, CacheEntry>();
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private NetworkCredential _apiCredential;
        private readonly String _apiUrl;
        private readonly Int32 cacheTimeout = 30;
        private ConcurrentDictionary<String, NetworkEntry> _networkCache;


        /// <summary>
        /// Provider for getting the network id from FL1
        /// </summary>
        /// <param name="contextFactory"></param>
        public NetworkIdProvider(FlexinetsEntitiesFactory contextFactory, String apiUrl)
        {
            _apiUrl = apiUrl;
            _contextFactory = contextFactory;
            _apiCredential = GetApiCredentials();
            _networkCache = LoadNetworks();
        }


        /// <summary>
        /// Load networks from database
        /// </summary>
        /// <returns></returns>
        private ConcurrentDictionary<string, NetworkEntry> LoadNetworks()
        {
            using (var db = _contextFactory.GetContext())
            {
                var networks = from o in db.Networks
                               select new NetworkEntry
                               {
                                   CountryName = o.countryname,
                                   NetworkId = o.mccmnc.ToString(),
                                   NetworkName = o.providername
                               };

                var directory = new ConcurrentDictionary<String, NetworkEntry>();
                foreach (var network in networks)
                {
                    directory.TryAdd(network.NetworkId, network);
                }
                return directory;
            }
        }


        /// <summary>
        /// Get the mccmnc for a msisdn
        /// </summary>
        /// <param name="msisdn"></param>
        /// <returns></returns>
        public String GetNetworkId(String msisdn)
        {
            String networkId;

            CacheEntry cacheEntry;
            _log.Debug($"Getting network id for msisdn {msisdn}");
            if (_networkIdCache.TryGetValue(msisdn, out cacheEntry))
            {
                _log.Debug($"Found cache entry {cacheEntry.NetworkId} for msisdn {msisdn}");
                if (DateTime.UtcNow.Subtract(cacheEntry.DateSet).TotalSeconds < cacheTimeout)
                {
                    _log.Debug($"Cache entry less than {cacheTimeout} seconds old!");
                    networkId = cacheEntry.NetworkId;
                }
                else
                {
                    networkId = GetId(msisdn);
                }
            }
            else
            {
                networkId = GetId(msisdn);
            }

            var entry = new CacheEntry { NetworkId = networkId, DateSet = DateTime.UtcNow };
            _networkIdCache.AddOrUpdate(msisdn, entry, (s, i) => entry);  // dafuk?

            _log.Debug($"Refreshed cache entry for msisdn {msisdn}, networkid {networkId}");
            return networkId;
        }


        private String GetId(String msisdn)
        {
            String networkId = null;
            var url = _apiUrl + msisdn;

            try
            {
                networkId = GetNetworkIdFromApi(networkId, url);
            }
            catch (WebException ex)
            {
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Unauthorized)
                {
                    _log.Warn("Got 401, refreshing API credentials from database and retrying");
                    _apiCredential = GetApiCredentials();
                    networkId = GetNetworkIdFromApi(networkId, url);
                }
                else
                {
                    _log.Fatal($"Could not get networkid for {msisdn}");
                    throw;
                }
            }
            catch (Exception)
            {
                _log.Fatal($"Could not get networkid for {msisdn}");
                throw;
            }


            if (!validNetwork(networkId))
            {
                _log.Fatal($"Got invalid networkid {networkId} from cache for msisdn {msisdn}");
                throw new InvalidOperationException($"Got invalid networkid {networkId} from cache for msisdn {msisdn}");
            }

            return networkId;
        }


        /// <summary>
        /// Optimistically verify that the network id returned from the API is valid
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns></returns>
        private Boolean validNetwork(String networkId)
        {
            if (_networkCache.ContainsKey(networkId))
            {
                return true;
            }

            // Dont take no for an answer, refresh list in case something has been added
            _networkCache = LoadNetworks();

            return _networkCache.ContainsKey(networkId);
        }


        /// <summary>
        /// GetNetworkIdFromApi
        /// </summary>
        /// <param name="networkId"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        private String GetNetworkIdFromApi(String networkId, String url)
        {
            var client = new WebClient { Credentials = _apiCredential };
            var response = client.DownloadString(url);

            var d = new XmlDocument();
            d.LoadXml(response);
            if (d.GetElementsByTagName("message")[0].InnerText == "ok")
            {
                networkId = d.GetElementsByTagName("MCC_MNC")[0].InnerText;
            }
            return networkId;
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