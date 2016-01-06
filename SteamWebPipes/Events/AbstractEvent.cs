using Newtonsoft.Json;

namespace SteamWebPipes
{
    [JsonObject(MemberSerialization.OptIn)]
	internal abstract class AbstractEvent
	{
        [JsonProperty]
        public readonly string Type;

        public AbstractEvent(string type)
        {
            Type = type;
        }
	}
}
