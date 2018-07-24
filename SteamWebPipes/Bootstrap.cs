using System;
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
            
            var server = new WebSocketServer(Config.Location);
            server.SupportedSubProtocols = new[] { "steam-pics" };

            if (File.Exists(Config.X509Certificate))
            {
                Log("Using certificate");
                server.Certificate = new X509Certificate2(Config.X509Certificate);
            }

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    ConnectedClients.Add(socket);

                    socket.ConnectionInfo.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor);
                    
                    Log($"Client #{ConnectedClients.Count} connected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort} ({forwardedFor})");

                    socket.Send(JsonConvert.SerializeObject(new UsersOnlineEvent(ConnectedClients.Count)));
                };

                socket.OnClose = () =>
                {
                    Log("Client #{2} disconnected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

                    ConnectedClients.Remove(socket);
                };
            });

            var steam = new Steam();

            if (File.Exists("last-changenumber.txt"))
            {
                steam.PreviousChangeNumber = uint.Parse(File.ReadAllText("last-changenumber.txt"));
            }

            var thread = new Thread(steam.Tick);
            thread.Name = "Steam";
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

                foreach (var socket in ConnectedClients.ToList())
                {
                    socket.Close();
                }

                server.Dispose();
            };
        }

        private static void TimerTick(object sender, ElapsedEventArgs e)
        {
            var users = ConnectedClients.Count;

            if (users == 0 || users == LastBroadcastConnectedUsers)
            {
                return;
            }

            LastBroadcastConnectedUsers = users;

            Broadcast(new UsersOnlineEvent(users));
        }

        public static void Broadcast(AbstractEvent ev)
        {
            Broadcast(JsonConvert.SerializeObject(ev));
        }

        private static void Broadcast(string message)
        {
            ConnectedClients.ForEach(socket => socket?.Send(message));
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
