using FlexinetsDBEF;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Flexinets.Radius
{
    public class NetworkProvider
    {
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private ConcurrentDictionary<String, NetworkEntry> _networkCache = new ConcurrentDictionary<String, NetworkEntry>();
        private readonly ILog _log = LogManager.GetLogger(typeof(NetworkProvider));


        public NetworkProvider(FlexinetsEntitiesFactory contextFactory)
        {
            _contextFactory = contextFactory;
            try
            {
                _networkCache = LoadNetworks();
            }
            catch (Exception ex)
            {
                _log.Error("Unable to load networks to cache, check db", ex);
            }
        }


        /// <summary>
        /// Load networks from database
        /// </summary>
        /// <returns></returns>
        private ConcurrentDictionary<String, NetworkEntry> LoadNetworks()
        {
            using (var db = _contextFactory.GetContext())
            {
                var networks = from o in db.Networks.ToList()
                               select new KeyValuePair<String, NetworkEntry>(o.mccmnc.ToString(), new NetworkEntry
                               {
                                   CountryName = o.countryname,
                                   NetworkId = o.mccmnc.ToString(),
                                   NetworkName = o.providername
                               });

                return new ConcurrentDictionary<String, NetworkEntry>(networks);
            }
        }


        /// <summary>
        /// Optimistically verify that the network id returned from the API is valid
        /// Valid means known to flexinets...
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns></returns>
        public Boolean IsValidNetwork(String networkId)
        {
            if (!_networkCache.ContainsKey(networkId))
            {
                // Dont take no for an answer, refresh list in case something has been added
                _networkCache = LoadNetworks();
            }

            return _networkCache.ContainsKey(networkId);
        }
    }
}
