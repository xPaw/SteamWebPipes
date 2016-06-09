using System;
using System.Threading;
using SteamKit2;
using System.Linq;

namespace SteamWebPipes
{
    internal class Steam
    {
        private readonly CallbackManager CallbackManager;
        private readonly SteamClient Client;
        private readonly SteamUser User;
        private readonly SteamApps Apps;
        private bool IsLoggedOn = false;

        public uint PreviousChangeNumber;
        public bool IsRunning = true;

        public Steam()
        {
            Client = new SteamClient();
            User = Client.GetHandler<SteamUser>();
            Apps = Client.GetHandler<SteamApps>();

            CallbackManager = new CallbackManager(Client);
            CallbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            CallbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            CallbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            CallbackManager.Subscribe<SteamApps.PICSChangesCallback>(OnPICSChanges);
        }

        public void Tick()
        {
            var loadServersTask = SteamDirectory.Initialize(0);
            loadServersTask.Wait();

            if (loadServersTask.IsFaulted)
            {
                Console.WriteLine("Error loading server list from directory: {0}", loadServersTask.Exception.Message);
                return;
            }

            Client.Connect();

            while (IsRunning)
            {
                CallbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(5));

                if (IsLoggedOn)
                {
                    Apps.PICSGetChangesSince(PreviousChangeNumber, true, true);
                }
            }
        }

        private void OnPICSChanges(SteamApps.PICSChangesCallback callback)
        {
            var previous = PreviousChangeNumber;

            if (previous == callback.CurrentChangeNumber)
            {
                return;
            }

            PreviousChangeNumber = callback.CurrentChangeNumber;

            var packageChangesCount = callback.PackageChanges.Count;
            var appChangesCount = callback.AppChanges.Count;

            Bootstrap.Log("Changelist {0} -> {1} ({2} apps, {3} packages)", PreviousChangeNumber, callback.CurrentChangeNumber, appChangesCount, packageChangesCount);

            if (previous == 0)
            {
                return;
            }

            // Group apps and package changes by changelist, this will seperate into individual changelists
            var appGrouping = callback.AppChanges.Values.GroupBy(a => a.ChangeNumber);
            var packageGrouping = callback.PackageChanges.Values.GroupBy(p => p.ChangeNumber);

            // Join apps and packages back together based on changelist number
            var changeLists = Utils.FullOuterJoin(appGrouping, packageGrouping, a => a.Key, p => p.Key, (a, p, key) => new SteamChangelist
                {
                    ChangeNumber = key,

                    Apps = a.Select(x => x.ID),
                    Packages = p.Select(x => x.ID),
                },
                new EmptyGrouping<uint, SteamApps.PICSChangesCallback.PICSChangeData>(),
                new EmptyGrouping<uint, SteamApps.PICSChangesCallback.PICSChangeData>())
                .OrderBy(c => c.ChangeNumber);

            foreach (var changeList in changeLists)
            {
                Bootstrap.Broadcast(new ChangelistEvent(changeList));
            }
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Bootstrap.Log("Could not connect to Steam: {0}", callback.Result);

                IsRunning = false;

                return;
            }

            Bootstrap.Log("Connected to Steam, logging in...");

            User.LogOnAnonymous();
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (!IsRunning)
            {
                Bootstrap.Log("Shutting down...");

                return;
            }

            if (IsLoggedOn)
            {
                Bootstrap.Broadcast(new LogOffEvent());

                IsLoggedOn = false;
            }

            Bootstrap.Log("Disconnected from Steam. Retrying...");

            Thread.Sleep(TimeSpan.FromSeconds(15));

            Client.Connect();
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Bootstrap.Log("Failed to login: {0}", callback.Result);

                Thread.Sleep(TimeSpan.FromSeconds(2));

                return;
            }

            IsLoggedOn = true;

            Bootstrap.Broadcast(new LogOnEvent());

            Bootstrap.Log("Logged in, current valve time is {0} UTC", callback.ServerTime);
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            if (IsLoggedOn)
            {
                Bootstrap.Broadcast(new LogOffEvent());

                IsLoggedOn = false;
            }

            Bootstrap.Log("Logged off from Steam");
        }
    }
}
