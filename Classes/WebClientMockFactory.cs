using System;
using System.Net;

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
            var client = new MockWebClient()
            {
                Response = Response
            };
            return client;
        }

        public IWebClient Create(NetworkCredential credential)
        {
            var client = Create();
            client.Credentials = credential;
            return client;
        }
    }
}
