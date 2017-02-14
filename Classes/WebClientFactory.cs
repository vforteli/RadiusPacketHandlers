namespace Flexinets.Radius
{
    public class WebClientFactory : IWebClientFactory
    {
        public IWebClient Create()
        {            
            return new WebClientWrapper();
        }
    }
}
