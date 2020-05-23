using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Timers;
using Fleck;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

namespace SteamWebPipes
{
    internal static class Bootstrap
    {
        public class Configuration
        {
            public string Location { get; set; }
            public string DatabaseConnectionString { get; set; }
            public string X509Certificate { get; set; }
        }

        private static int LastBroadcastConnectedUsers;
        private static readonly List<IWebSocketConnection> ConnectedClients = new List<IWebSocketConnection>();
        private static readonly ConcurrentDictionary<IWebSocketConnection, List<uint>> SubscribedAppClients = new ConcurrentDictionary<IWebSocketConnection, List<uint>>();
        public static Configuration Config { get; private set; }

        private static void Main()
        {
            Console.Title = "SteamWebPipes";

            Config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location), "settings.json")));

            if (string.IsNullOrWhiteSpace(Config.DatabaseConnectionString))
            {
                Config.DatabaseConnectionString = null;

                Log("Database connectiong string is empty, will not try to get app names");
            }

            var server = new WebSocketServer(Config.Location)
            {
                SupportedSubProtocols = new[] {"steam-pics"}
            };

            if (File.Exists(Config.X509Certificate))
            {
                Log("Using certificate");
                server.Certificate = new X509Certificate2(Config.X509Certificate);
            }

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    var apps = new List<uint>();

                    foreach (var sub in socket.ConnectionInfo.SubProtocol.Split(','))
                    {
                        if (sub.Trim().StartsWith("app-"))
                        {
                            if (!uint.TryParse(sub.Substring(5), out var appid) || apps.Count > 50)
                            {
                                socket.Close();
                                return;
                            }

                            apps.Add(appid);
                        }
                    }

                    socket.ConnectionInfo.Headers.TryGetValue("X-Forwarded-For", out var clientIp);

                    if (string.IsNullOrEmpty(clientIp))
                    {
                        clientIp = $"{socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}";
                    }

                    if (apps.Count > 0)
                    {
                        SubscribedAppClients.TryAdd(socket, apps);

                        Log($"Subscriber #{SubscribedAppClients.Count} connected: {clientIp} (Apps: {string.Join(", ", apps)})");

                        return;
                    }

                    lock (ConnectedClients)
                    {
                        ConnectedClients.Add(socket);
                    }

                    socket.Send(JsonConvert.SerializeObject(new UsersOnlineEvent(ConnectedClients.Count)));

                    if (ConnectedClients.Count < 500)
                    {
                        Log($"Client #{ConnectedClients.Count} connected: {clientIp}");
                    }
                };

                socket.OnClose = () =>
                {
                    lock (ConnectedClients)
                    {
                        ConnectedClients.Remove(socket);
                    }

                    SubscribedAppClients.TryRemove(socket, out _);
                };
            });

            var steam = new Steam();

            if (File.Exists("last-changenumber.txt"))
            {
                steam.PreviousChangeNumber = uint.Parse(File.ReadAllText("last-changenumber.txt"));
            }

            var thread = new Thread(steam.Tick)
            {
                Name = "Steam"
            };
            thread.Start();

            var timer = new Timer();
            timer.Elapsed += TimerTick;
            timer.Interval = TimeSpan.FromSeconds(30).TotalMilliseconds;
            timer.Start();

            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("Ctrl + C detected, shutting down.");
                File.WriteAllText("last-changenumber.txt", steam.PreviousChangeNumber.ToString());

                steam.IsRunning = false;
                timer.Stop();

                ConnectedClients.ToList().ForEach(socket => socket?.Close());

                server.Dispose();

                Environment.Exit(0);
            };
        }

        private static void TimerTick(object sender, ElapsedEventArgs e)
        {
            var users = ConnectedClients.Count;

            Broadcast(new UsersOnlineEvent(users));

            if (users == 0 || users == LastBroadcastConnectedUsers)
            {
                return;
            }

            LastBroadcastConnectedUsers = users;

            Log($"{users} users connected, {SubscribedAppClients.Count} app subscribers");
        }

        public static void SendAppsToSubscribers(Dictionary<uint, SteamKit2.SteamApps.PICSChangesCallback.PICSChangeData> apps)
        {
            foreach (var (socket, subscribedApps) in SubscribedAppClients)
            {
                var matches = apps.Keys.Intersect(subscribedApps);

                foreach (var appid in matches)
                {
                    _ = socket.Send(JsonConvert.SerializeObject(new AppUpdateEvent(appid, apps[appid].ChangeNumber)));
                }
            }
        }

        public static void Broadcast(AbstractEvent ev)
        {
            Broadcast(JsonConvert.SerializeObject(ev));
        }

        private static void Broadcast(string message)
        {
            lock (ConnectedClients)
            {
                for (var i = ConnectedClients.Count - 1; i >= 0; i--)
                {
                    var socket = ConnectedClients[i];

                    if (socket == null)
                    {
                        continue;
                    }

                    if (!socket.IsAvailable)
                    {
                        ConnectedClients.RemoveAt(i);
                        continue;
                    }

                    socket.Send(message);
                }
            }
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
