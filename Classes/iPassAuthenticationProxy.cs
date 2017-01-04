using FlexinetsDBEF;
using log4net;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Flexinets.Radius.PacketHandlers
{
    public class iPassAuthenticationProxy
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(iPassAuthenticationProxy));
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly String _oldPath;
        private readonly String _newPath;


        public iPassAuthenticationProxy(FlexinetsEntitiesFactory contextFactory, String oldPath, String newPath)
        {
            _contextFactory = contextFactory;
            _oldPath = oldPath;
            _newPath = newPath;
        }


        /// <summary>
        /// Proxy iPass authentication to an external server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public PacketCode? ProxyAuthentication(String username, String password)
        {
            var usernamedomain = UsernameDomain.Parse(username);

            using (var db = _contextFactory.GetContext())
            {
                var server = db.Roamservers.FirstOrDefault(o => o.domain == usernamedomain.Domain);
                if (server != null)
                {
                    _log.Debug($"Found proxy server {server.host} for username {username}");

                    ProcessStartInfo startinfo;
                    if (server.uselegacy)
                    {
                        startinfo = ProxyAuthenticationSsl(username, password, server.host);
                    }
                    else
                    {
                        startinfo = ProxyAuthenticationNew(username, password, server.host);
                    }

                    using (var process = new Process
                    {
                        StartInfo = startinfo
                    })
                    {
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
                            _log.Warn($"MemberOf failed for user {username}");
                        }
                        if (content.Contains("Message: LDAP search found no entries for this user"))
                        {
                            _log.Warn($"Username {username} not found");
                        }
                    }

                    // todo add logging for bad password?
                    return PacketCode.AccessReject;
                }
            }

            return null;
        }


        /// <summary>
        /// Proxy authentication to another roamserver using checkipass.bat
        /// </summary>
        /// <param name="usernamedomain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private ProcessStartInfo ProxyAuthenticationNew(String usernamedomain, String password, String host)
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {_newPath} -u {usernamedomain} -p {password} -host {host} -type auth",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }


        /// <summary>
        /// Used for older SSL type connections
        /// Uses checkipass.exe instead
        /// </summary>
        /// <param name="usernamedomain"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private ProcessStartInfo ProxyAuthenticationSsl(String usernamedomain, String password, String host)
        {
            return new ProcessStartInfo
            {
                FileName = _oldPath,
                Arguments = $" -u {usernamedomain} -p {password} -host {host} -type auth",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
    }
}