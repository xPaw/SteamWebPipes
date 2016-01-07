using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SteamKit2;

namespace SteamWebPipes
{
    internal class ChangelistEvent : AbstractEvent
    {
        [JsonProperty]
        public readonly uint ChangeNumber;

        [JsonProperty]
        public readonly Dictionary<uint, string> Apps;

        [JsonProperty]
        public readonly Dictionary<uint, string> Packages;

        public ChangelistEvent(SteamApps.PICSChangesCallback callback)
            : base("Changelist")
        {
            ChangeNumber = callback.CurrentChangeNumber;

            if (callback.AppChanges.Any())
            {
                Apps = callback.AppChanges.ToDictionary(x => x.Key, x => "Unknown App");

                try
                {
                    var apps = string.Join(",", callback.AppChanges.Keys);

                    using (var reader = MySqlHelper.ExecuteReader(Bootstrap.DatabaseConnectionString, "SELECT `AppID`, `Name`, `LastKnownName` FROM `Apps` WHERE `AppID` IN(" + apps + ")"))
                    {
                        while (reader.Read())
                        {
                            var name = reader.GetString(1);
                            var lastKnownName = reader.GetString(2);

                            if (!string.IsNullOrEmpty(lastKnownName) && name != lastKnownName)
                            {
                                name = string.Format("{0} ({1})", name, lastKnownName);
                            }

                            Apps[reader.GetUInt32(0)] = name;
                        }
                    }
                }
                catch (MySqlException e)
                {
                    Bootstrap.Log("{0}", e.Message);
                }
            }
            else
            {
                Apps = new Dictionary<uint, string>();
            }

            if (callback.PackageChanges.Any())
            {
                Packages = callback.PackageChanges.ToDictionary(x => x.Key, x => "Unknown Package");

                try
                {
                    var subs = string.Join(",", callback.PackageChanges.Keys);

                    using (var reader = MySqlHelper.ExecuteReader(Bootstrap.DatabaseConnectionString, "SELECT `SubID`, `LastKnownName` FROM `Subs` WHERE `SubID` IN(" + subs + ")"))
                    {
                        while (reader.Read())
                        {
                            Packages[reader.GetUInt32(0)] = reader.GetString(1);
                        }
                    }
                }
                catch (MySqlException e)
                {
                    Bootstrap.Log("{0}", e.Message);
                }
            }
            else
            {
                Packages = new Dictionary<uint, string>();
            }
        }
    }
}
