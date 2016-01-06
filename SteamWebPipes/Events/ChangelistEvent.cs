using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SteamKit2;

namespace SteamWebPipes
{
    internal class ChangelistEvent : AbstractEvent
    {
        [JsonProperty]
        public readonly uint ChangeNumber;

        [JsonProperty]
        public readonly List<uint> Apps;

        [JsonProperty]
        public readonly List<uint> Packages;

        public ChangelistEvent(SteamApps.PICSChangesCallback callback)
            : base("Changelist")
        {
            ChangeNumber = callback.CurrentChangeNumber;

            Apps = callback.AppChanges.Keys.ToList();
            Packages = callback.PackageChanges.Keys.ToList();
        }
    }
}
