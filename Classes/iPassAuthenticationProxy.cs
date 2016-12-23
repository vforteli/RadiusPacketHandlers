using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flexinets.Radius.PacketHandlers
{
    public class iPassAuthenticationProxy
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(iPassAuthenticationProxy));
        private readonly String _oldPath;
        private readonly String _newPath;
        public readonly Dictionary<String, iPassServerInfo> Servers = new Dictionary<String, iPassServerInfo>();


        public iPassAuthenticationProxy(String oldPath, String newPath)
        {
            _oldPath = oldPath; // @"c:\ipass\roamserver\5.2.1\test\checkipass.exe"
            _newPath = newPath; // @"c:\ipass\roamserver\6.1.0\test\checkipass.bat";
        }


        public PacketCode ProxyAuthentication(String username, String password)
        {
            var usernamedomain = Utils.SplitUsernameDomain(username);
            if (Servers.ContainsKey(usernamedomain.Domain))
            {
                var server = Servers[usernamedomain.Domain];
                if (server.UseLegacy)
                {
                    return ProxyAuthenticationSsl(username, password, server.host);
                }
                else
                {
                    return ProxyAuthenticationNew(username, password, server.host);
                }
            }

            _log.Error($"No proxy server found for username {username}");
            return PacketCode.AccessReject;
        }



        /// <summary>
        /// Proxy authentication to another roamserver using checkipass.bat
        /// </summary>
        /// <param name="usernamedomain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private PacketCode ProxyAuthenticationNew(String usernamedomain, String password, String host)
        {
            // todo add indefinite password caching?
            var path = $"/C {_newPath} -u {usernamedomain} -p {password} -host {host} -type auth";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe", path)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var sb = new StringBuilder();
            process.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.StandardInput.WriteLine();  // Exits the script
            process.WaitForExit();

            var content = sb.ToString();
            _log.Debug(content);

            if (content.Contains("Status: accept"))
            {
                return PacketCode.AccessAccept;
            }
            return PacketCode.AccessReject;
        }


        /// <summary>
        /// Used for older SSL type connections
        /// Uses checkipass.exe instead
        /// </summary>
        /// <param name="usernamedomain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private PacketCode ProxyAuthenticationSsl(String usernamedomain, String password, String host)
        {
            // todo add indefinite password caching?
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _oldPath,
                    Arguments = $" -u {usernamedomain} -p {password} -host {host} -type auth",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var sb = new StringBuilder();
            process.OutputDataReceived += (sender, args) => sb.AppendLine(args.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.StandardInput.WriteLine();  // Exits the script
            process.WaitForExit();

            var content = sb.ToString();

            _log.Debug(content);

            if (content.Contains("Status: accept"))
            {
                return PacketCode.AccessAccept;
            }

            if (content.Contains("LDAP User found but memberOf validation failed"))
            {
                _log.Info($"MemberOf failed for user {usernamedomain}");
            }

            // todo add logging for bad password?
            // todo add logging for non-existent users?
            return PacketCode.AccessReject;
        }
    }
}