namespace SteamWebPipes.Events
{
	internal abstract class AbstractEvent
	{
        public string Type { get; private set; }

        protected AbstractEvent(string type)
        {
            Type = type;
        }
	}
}
