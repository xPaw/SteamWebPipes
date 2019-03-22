using Newtonsoft.Json;

namespace SteamWebPipes
{
    [JsonObject(MemberSerialization.OptIn)]
	internal abstract class AbstractEvent
	{
        [JsonProperty]
        public readonly string Type;

        protected AbstractEvent(string type)
        {
            Type = type;
        }
	}
}
