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
        private static int LastBroadcastConnectedUsers;
        private static List<IWebSocketConnection> ConnectedClients = new List<IWebSocketConnection>();
        public static string DatabaseConnectionString;

        private static void Main(string[] args)
        {
            if (File.Exists("database.txt"))
            {
                Log("Using database connection string");

                DatabaseConnectionString = File.ReadAllText("database.txt").Trim();
            }
            else
            {
                Log("database.txt does not exist, will not try to use database");
            }

            var useCert = File.Exists("cert.pfx");

            var server = new WebSocketServer("ws" + (useCert ? "s" : "") + "://0.0.0.0:8181");
            server.SupportedSubProtocols = new[] { "steam-pics" };

            if (useCert)
            {
                Log("Using certificate");
                server.Certificate = new X509Certificate2("cert.pfx");
            }

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    ConnectedClients.Add(socket);

                    Log("Client #{2} connected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

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

            Console.CancelKeyPress += delegate {
                Console.WriteLine("Ctrl + C detected, shutting down.");
                File.WriteAllText("last-changenumber.txt", steam.PreviousChangeNumber.ToString());

                steam.IsRunning = false;
                thread.Abort();
                timer.Stop();

                foreach (var socket in ConnectedClients.ToList()) {
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
            for (int i = ConnectedClients.Count - 1; i >= 0; i--)
            {
                var socket = ConnectedClients[i];

                if (!socket.IsAvailable)
                {
                    Log("Removing dead client #{2}: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);

                    ConnectedClients.RemoveAt(i);

                    continue;
                }

                socket.Send(message);
            }
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
