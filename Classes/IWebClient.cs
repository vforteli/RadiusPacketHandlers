using System;
using System.Net;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public interface IWebClient : IDisposable
    {
        ICredentials Credentials { get; set; }

        Task<String> DownloadStringTaskAsync(String url);
    }
}
