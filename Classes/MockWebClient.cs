using System;
using System.Net;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class MockWebClient : IWebClient
    {
        public ICredentials Credentials
        {
            get; set;
        }

        public String Response
        {
            get; set;
        }

        public void Dispose()
        {
            // Nothing to do...
        }

        public Task<String> DownloadStringTaskAsync(String url)
        {
            return Task<String>.Factory.StartNew(() => { return Response; });
        }
    }
}
