using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class UsernameDomain
    {
        public String Username;
        public String Domain;

        public String FullUsername
        {
            get
            {
                return $"{Username}@{Domain}";
            }
        }
    }

    public static class Utils
    {
        public static UsernameDomain SplitUsernameDomain(String rawusername)
        {
            return new UsernameDomain
            {
                Username = rawusername.Substring(0, rawusername.LastIndexOf('@')),
                Domain = rawusername.Substring(rawusername.LastIndexOf('@') + 1)
            };
        }
    }
}
