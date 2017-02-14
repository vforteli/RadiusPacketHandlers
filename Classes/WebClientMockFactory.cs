using System;

namespace Flexinets.Radius
{
    public class WebClientMockFactory : IWebClientFactory
    {
        public String Response
        {
            get; set;
        }

        public IWebClient Create()
        {
            var client = new MockWebClient();
            client.Response = Response;
            return client;
        }
    }
}
