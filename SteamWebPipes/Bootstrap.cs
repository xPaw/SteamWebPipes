using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fleck;
using Newtonsoft.Json;

namespace SteamWebPipes
{
    internal static class Bootstrap
    {
        private static List<IWebSocketConnection> ConnectedClients = new List<IWebSocketConnection>();

        private static void Main(string[] args)
        {
            FleckLog.Level = LogLevel.Debug;

            var server = new WebSocketServer("ws://0.0.0.0:8181");
            server.SupportedSubProtocols = new[] { "steam-pics" };
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    ConnectedClients.Add(socket);

                    Log("Client #{2} connected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);
                };
                socket.OnClose = () =>
                {
                    ConnectedClients.Remove(socket);

                    Log("Client #{2} disconnected: {0}:{1}", socket.ConnectionInfo.ClientIpAddress, socket.ConnectionInfo.ClientPort, ConnectedClients.Count);
                };
            });

            var thread = new Thread(new Steam().Tick);
            thread.Name = "Steam";
            thread.Start();

            Console.ReadLine();
        }

        public static void Broadcast(AbstractEvent ev)
        {
            Broadcast(JsonConvert.SerializeObject(ev));
        }

        private static void Broadcast(string message)
        {
            foreach (var socket in ConnectedClients)
            {
                socket.Send(message);
            }
        }

        public static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString("R") + "] " + string.Format(format, args));
        }
    }
}
