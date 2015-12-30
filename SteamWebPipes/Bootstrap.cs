using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fleck;

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
                    Log("Open! {0}", socket.ConnectionInfo.ClientIpAddress);
                    ConnectedClients.Add(socket);
                    socket.Send("hello world");
                };
                socket.OnClose = () =>
                {
                    Log("Close! {0}", socket.ConnectionInfo.ClientIpAddress);
                    ConnectedClients.Remove(socket);
                };
            });

            var thread = new Thread(new Steam().Tick);
            thread.Name = "Steam";
            thread.Start();

            Console.ReadLine();
        }

        public static void Broadcast(string message)
        {
            foreach (var socket in ConnectedClients.ToList())
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
