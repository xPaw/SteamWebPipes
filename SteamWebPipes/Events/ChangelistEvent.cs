using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

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

        public ChangelistEvent(SteamChangelist changelist)
            : base("Changelist")
        {
            ChangeNumber = changelist.ChangeNumber;

            if (changelist.Apps.Any())
            {
                Apps = changelist.Apps.ToDictionary(x => x, x => "Unknown App " + x);

                if (Bootstrap.Config.DatabaseConnectionString != null)
                {
                    try
                    {
                        var apps = string.Join(",", changelist.Apps);

                        using (var reader = MySqlHelper.ExecuteReader(Bootstrap.Config.DatabaseConnectionString, "SELECT `AppID`, `Name`, `LastKnownName` FROM `Apps` WHERE `AppID` IN(" + apps + ")"))
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
            }
            else
            {
                Apps = new Dictionary<uint, string>();
            }

            if (changelist.Packages.Any())
            {
                Packages = changelist.Packages.ToDictionary(x => x, x => "Unknown Package " + x);

                if (Bootstrap.Config.DatabaseConnectionString != null)
                {
                    try
                    {
                        var subs = string.Join(",", changelist.Packages);

                        using (var reader = MySqlHelper.ExecuteReader(Bootstrap.Config.DatabaseConnectionString, "SELECT `SubID`, `LastKnownName` FROM `Subs` WHERE `SubID` IN(" + subs + ")"))
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
            }
            else
            {
                Packages = new Dictionary<uint, string>();
            }
        }
    }
}
