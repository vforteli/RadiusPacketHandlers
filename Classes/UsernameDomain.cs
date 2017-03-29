using System;
using System.Linq;

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


        /// <summary>
        /// Parse a raw username and optionally trim additional domains from the username part
        /// </summary>
        /// <param name="rawusername"></param>
        /// <param name="stripUsername"></param>
        /// <returns></returns>
        public static UsernameDomain Parse(String rawusername, Boolean stripUsername)
        {
            var usernamedomain = Parse(rawusername);
            if (stripUsername && usernamedomain.Username.Contains('@'))
            {
                usernamedomain.Username = usernamedomain.Username.Substring(0, usernamedomain.Username.IndexOf('@'));
            }        

            return usernamedomain;
        }


        /// <summary>
        /// Parse a raw username string and split
        /// </summary>
        /// <param name="rawusername"></param>
        /// <returns></returns>
        public static UsernameDomain Parse(String rawusername)
        {
            if (rawusername.Contains("@"))
            {
                return new UsernameDomain
                {
                    Username = rawusername.Substring(0, rawusername.LastIndexOf('@')),
                    Domain = rawusername.Substring(rawusername.LastIndexOf('@') + 1)
                };
            }
            else
            {
                return new UsernameDomain { Username = rawusername, Domain = "flexinets" };
            }
            // todo 2016-12-05 make sure this doesnt screw anything up, quick hack for mbb
        }


        /// <summary>
        /// Returns the username@domain
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Username ?? ""}@{Domain ?? ""}";
        }
    }
}