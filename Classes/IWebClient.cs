using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public interface IWebClient : IDisposable
    {
        ICredentials Credentials { get; set; }

        Task<String> DownloadStringTaskAsync(String url);
    }
}
