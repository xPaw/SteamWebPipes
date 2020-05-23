using System.Collections.Generic;

namespace SteamWebPipes
{
    internal class SteamChangelist
    {
        public uint ChangeNumber { get; set; }
        public IEnumerable<uint> Apps { get; set; }
        public IEnumerable<uint> Packages { get; set; }
    }
}
