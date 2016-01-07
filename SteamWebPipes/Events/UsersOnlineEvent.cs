using Newtonsoft.Json;

namespace SteamWebPipes
{
    internal class UsersOnlineEvent : AbstractEvent
    {
        [JsonProperty]
        public readonly int Users;

        public UsersOnlineEvent(int num)
            : base("UsersOnline")
        {
            Users = num;
        }
    }
}
