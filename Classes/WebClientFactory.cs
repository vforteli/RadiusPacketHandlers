using System;
using System.Net;

namespace Flexinets.Radius
{
    public class WebClientFactory : IWebClientFactory
    {
        public IWebClient Create()
        {            
            return new WebClientWrapper();
        }

        public IWebClient Create(NetworkCredential credential)
        {
            var client = new WebClientWrapper();
            client.Credentials = credential;
            return client;
        }
    }
}
