using Flexinets.Radius;
using Flexinets.Radius.PacketHandlers;
using FlexinetsDBEF;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace RadiusServerTests
{
    [TestClass]
    public class NetworkIdTests
    {     
        private NetworkIdProvider GetNetworkIdProvider(DateTimeProvider dateTimeProvider)
        {
            var _contextFactory = new FlexinetsEntitiesFactory("Data Source=XANADU;Initial Catalog=flexinets;Integrated Security=True");
            var networkProvider = new NetworkProvider(_contextFactory);
            var networkApiClient = new NetworkApiClient(_contextFactory, new WebClientFactory(), networkProvider, "http://localhost:8500/api/networkid/?msisdn=");
            var networkIdProvider = new NetworkIdProvider(new DateTimeProvider(), networkApiClient);
            return networkIdProvider;
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException), "Got invalid networkid 528501 for msisdn 436600000018")]
        public void TestNetworkIdFail()
        {
            var foo = GetNetworkIdProvider(new DateTimeProvider());
            var id = foo.GetNetworkId("436600000018");
            Assert.AreEqual("528501", id);
        }


        [TestMethod]
        public void TestNetworkIdSuccess()
        {
            var foo = GetNetworkIdProvider(new DateTimeProvider());
            var id = foo.GetNetworkId("43660000001");
            Assert.AreEqual("24491", id);
        }


        [TestMethod]
        public void TestNetworkIdSuccessPasswordUpdate()
        {
            var _contextFactory = new FlexinetsEntitiesFactory("Data Source=XANADU;Initial Catalog=flexinets;Integrated Security=True");
            var networkProvider = new NetworkProvider(_contextFactory);
            var networkApiClient = new NetworkApiClient(_contextFactory, new WebClientFactory(), networkProvider, "http://localhost:8500/api/networkid/?msisdn=");
            networkApiClient.ApiCredential = new NetworkCredential("hurr", "durr");
            var networkIdProvider = new NetworkIdProvider(new DateTimeProvider(), networkApiClient);

            var id = networkIdProvider.GetNetworkId("43660000001");
            Assert.AreEqual("24491", id);
        }


        [TestMethod]
        public void TestNetworkIdSuccessDuplicate()
        {
            var foo = GetNetworkIdProvider(new DateTimeProvider());
            var results = new List<String>();

            Parallel.For(1, 5, o =>
            {
                results.Add(foo.GetNetworkId("4366000000delay"));
            });

            foreach (var result in results)
            {
                Assert.AreEqual("24491", result);
            }
        }


        [TestMethod]
        public void TestNetworkIdSuccessDuplicateParallel()
        {
            var foo = GetNetworkIdProvider(new DateTimeProvider());
            var id = foo.GetNetworkId("4366000000delay");
            var id2 = foo.GetNetworkId("4366000000delay");
            var id3 = foo.GetNetworkId("4366000000delay");
            Assert.AreEqual("24491", id);
        }


        [TestMethod]
        [ExpectedException(typeof(AggregateException), "Api failed, see logs for details")]
        public void TestNetworkIdApiFail()
        {
            var foo = GetNetworkIdProvider(new DateTimeProvider());
            var id = foo.GetNetworkId("43660000002");
            Assert.AreEqual("528501", id);
        }


        [TestMethod]
        public void TestTryGetNetworkIdFail()
        {
            var clientFactory = new WebClientMockFactory();

            // Set fail response
            clientFactory.Response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <getLocation_Response xmlns=""http://pos.mbb-world.com"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                        <header>
                            <error>105</error>
                            <type>warning</type>
                            <message>VLR Lookup failed</message>
                            <description>webconnect returned REJECT</description>
                            <datetime>2017-01-04T09:44:44.557019+01:00</datetime>
                        </header>
                    </getLocation_Response>";

            var failMsisdn = "43660000000";
            var dateTimeProvider = new MockDateTimeProvider();
            var startTime = new DateTime(2016, 1, 1);
            dateTimeProvider.SetDateTimeUtcNow(startTime);

            var _contextFactory = new FlexinetsEntitiesFactory("Data Source=XANADU;Initial Catalog=flexinets;Integrated Security=True");

            var networkProvider = new NetworkProvider(_contextFactory);
            var networkApiClient = new NetworkApiClient(_contextFactory, clientFactory, networkProvider, "http://localhost:8500/api/networkid/?msisdn=");
            var _networkIdProvider = new NetworkIdProvider(dateTimeProvider, networkApiClient);

            String networkId;
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            // Change to success response
            clientFactory.Response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <getLocation_Response xmlns=""http://pos.mbb-world.com"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                        <header>
                            <error>0</error>
                            <type>none</type>
                            <message>ok</message>
                            <description/>
                            <datetime>2015-09-30T09:36:05.6586193+02:00</datetime>
                        </header>
                        <VLR_address>6593340088</VLR_address>
                        <MCC_MNC>24491</MCC_MNC>
                    </getLocation_Response>";
            

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            var success = _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);
            Assert.IsFalse(success);
        }


        [TestMethod]
        public void TestTryGetNetworkIdSuccess()
        {
            var clientFactory = new WebClientMockFactory();

            // Set fail response
            clientFactory.Response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <getLocation_Response xmlns=""http://pos.mbb-world.com"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                        <header>
                            <error>105</error>
                            <type>warning</type>
                            <message>VLR Lookup failed</message>
                            <description>webconnect returned REJECT</description>
                            <datetime>2017-01-04T09:44:44.557019+01:00</datetime>
                        </header>
                    </getLocation_Response>";

            var failMsisdn = "43660000000";
            var dateTimeProvider = new MockDateTimeProvider();
            var startTime = new DateTime(2016, 1, 1);
            dateTimeProvider.SetDateTimeUtcNow(startTime);

            var _contextFactory = new FlexinetsEntitiesFactory("Data Source=XANADU;Initial Catalog=flexinets;Integrated Security=True");

            var networkProvider = new NetworkProvider(_contextFactory);
            var networkApiClient = new NetworkApiClient(_contextFactory, clientFactory, networkProvider, "http://duh");
            var _networkIdProvider = new NetworkIdProvider(dateTimeProvider, networkApiClient);


            String networkId;
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(26));
            _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);

            // Change to success response
            clientFactory.Response = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <getLocation_Response xmlns=""http://pos.mbb-world.com"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
                        <header>
                            <error>0</error>
                            <type>none</type>
                            <message>ok</message>
                            <description/>
                            <datetime>2015-09-30T09:36:05.6586193+02:00</datetime>
                        </header>
                        <VLR_address>6593340088</VLR_address>
                        <MCC_MNC>24491</MCC_MNC>
                    </getLocation_Response>";


            dateTimeProvider.SetDateTimeUtcNow(dateTimeProvider.UtcNow.AddSeconds(27));
            var success = _networkIdProvider.TryGetNetworkId(failMsisdn, out networkId);
            Assert.IsTrue(success);
        }
    }
}
