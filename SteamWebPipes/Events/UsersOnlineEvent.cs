namespace SteamWebPipes.Events
{
    internal class UsersOnlineEvent : AbstractEvent
    {
        public int Users { get; private set; }

        public UsersOnlineEvent(int num)
            : base("UsersOnline")
        {
            Users = num;
        }
    }
}
