using Newtonsoft.Json;

namespace SteamWebPipes
{
    internal class AppUpdateEvent : AbstractEvent
    {
        [JsonProperty]
        public readonly uint AppID;

        [JsonProperty]
        public readonly uint ChangeNumber;

        public AppUpdateEvent(uint appid, uint changenumber)
            : base("AppUpdate")
        {
            AppID = appid;
            ChangeNumber = changenumber;
        }
    }
}
