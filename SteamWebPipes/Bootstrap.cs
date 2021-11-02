using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Timers;
using Fleck;
using SteamWebPipes.Events;
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
        private static readonly List<IWebSocketConnection> ConnectedClients = new();
        public static Configuration Config { get; private set; }

        private static void Main()
        {
            Console.Title = "SteamWebPipes";

            AppDomain.CurrentDomain.UnhandledException += OnSillyCrashHandler;

            Config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "settings.json")));

            if (string.IsNullOrWhiteSpace(Config.DatabaseConnectionString))
            {
                Config.DatabaseConnectionString = null;

                Log("Database connectiong string is empty, will not try to get app names");
            }

            var server = new WebSocketServer(Config.Location)
            {
                SupportedSubProtocols = new[] { "steam-pics" }
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
                    int users;

                    lock (ConnectedClients)
                    {
                        ConnectedClients.Add(socket);
                        users = ConnectedClients.Count;
                    }

                    socket.Send(JsonSerializer.Serialize(new UsersOnlineEvent(users)));

                    if (users >= 500)
                    {
                        return;
                    }

                    socket.ConnectionInfo.Headers.TryGetValue("X-Forwarded-For", out var clientIp);

                    if (string.IsNullOrEmpty(clientIp))
                    {
                        clientIp = $"{socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}";
                    }

                    Log($"Client #{users} connected: {clientIp}");
                };

                socket.OnClose = () =>
                {
                    lock (ConnectedClients)
                    {
                        ConnectedClients.Remove(socket);
                    }
                };
            });

            var steam = new Steam();

            if (File.Exists("last-changenumber.txt"))
            {
                steam.PreviousChangeNumber = uint.Parse(File.ReadAllText("last-changenumber.txt"));
            }

            var timer = new Timer();
            timer.Elapsed += TimerTick;
            timer.Interval = TimeSpan.FromSeconds(30).TotalMilliseconds;
            timer.Start();

            void Exit()
            {
                File.WriteAllText("last-changenumber.txt", steam.PreviousChangeNumber.ToString());

                steam.IsRunning = false;
                timer.Stop();

                lock (ConnectedClients)
                {
                    ConnectedClients.ToList().ForEach(socket => socket?.Close());
                }

                server.Dispose();
            }

            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("Ctrl + C detected, shutting down.");

                Exit();

                Environment.Exit(0);
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                Exit();
            };

            steam.Tick();
        }

        private static void TimerTick(object sender, ElapsedEventArgs e)
        {
            int users;

            lock (ConnectedClients)
            {
                users = ConnectedClients.Count;
            }

            Broadcast(new UsersOnlineEvent(users));

            if (users == 0 || users == LastBroadcastConnectedUsers)
            {
                return;
            }

            LastBroadcastConnectedUsers = users;

            Log($"{users} users connected");
        }

        public static void Broadcast<T>(T ev)
        {
            var message = JsonSerializer.Serialize(ev);

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

        private static void OnSillyCrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            var parentException = args.ExceptionObject as Exception;

            if (parentException is AggregateException aggregateException)
            {
                aggregateException.Flatten().Handle(e =>
                {
                    Log("[UnhandledException] {0}", parentException);

                    return false;
                });
            }
            else
            {
                Log("[UnhandledException] {0}", parentException);
            }

            if (args.IsTerminating)
            {
                AppDomain.CurrentDomain.UnhandledException -= OnSillyCrashHandler;
            }
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
