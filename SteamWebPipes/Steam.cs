using System;
using System.Threading;
using SteamKit2;

namespace SteamWebPipes
{
    internal class Steam
    {
        private readonly CallbackManager CallbackManager;
        private readonly SteamClient Client;
        private readonly SteamUser User;
        private readonly SteamApps Apps;
        private uint PreviousChangeNumber;
        private bool IsRunning = true;
        private bool IsLoggedOn = false;

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
            if (PreviousChangeNumber == callback.CurrentChangeNumber)
            {
                return;
            }

            var packageChangesCount = callback.PackageChanges.Count;
            var appChangesCount = callback.AppChanges.Count;

            Bootstrap.Log("Changelist {0} -> {1} ({2} apps, {3} packages)", PreviousChangeNumber, callback.CurrentChangeNumber, appChangesCount, packageChangesCount);

            if (PreviousChangeNumber > 0)
            {
                Bootstrap.Broadcast(new ChangelistEvent(callback));
            }

            PreviousChangeNumber = callback.CurrentChangeNumber;
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
                Bootstrap.Broadcast(new GenericEvent("LogOff"));

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

            Bootstrap.Broadcast(new GenericEvent("LogOn"));

            Bootstrap.Log("Logged in, current valve time is {0} UTC", callback.ServerTime);
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            if (IsLoggedOn)
            {
                Bootstrap.Broadcast(new GenericEvent("LogOff"));

                IsLoggedOn = false;
            }

            Bootstrap.Log("Logged off from Steam");
        }
    }
}
