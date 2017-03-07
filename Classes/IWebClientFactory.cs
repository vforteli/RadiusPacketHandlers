using System.Net;

namespace Flexinets.Radius
{
    public interface IWebClientFactory
    {
        IWebClient Create();

        IWebClient Create(NetworkCredential credential);
    }
}