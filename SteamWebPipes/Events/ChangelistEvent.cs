using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Dapper;

namespace SteamWebPipes
{
    internal class ChangelistEvent : AbstractEvent
    {
        private struct AppData
        {
            public uint AppID { get; set; }
            public string Name { get; set; }
            public string LastKnownName { get; set; }
        }

        private struct PackageData
        {
            public uint SubID { get; set; }
            public string LastKnownName { get; set; }
        }

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
                        using var db = new MySqlConnection(Bootstrap.Config.DatabaseConnectionString);

                        foreach (var app in db.Query<AppData>("SELECT `AppID`, `Name`, `LastKnownName` FROM `Apps` WHERE `AppID` IN @Apps", new { changelist.Apps }))
                        {
                            if (!string.IsNullOrEmpty(app.LastKnownName) && app.Name != app.LastKnownName)
                            {
                                Apps[app.AppID] = $"{app.Name} ({app.LastKnownName})";
                            }
                            else
                            {
                                Apps[app.AppID] = app.Name;
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
                        using var db = new MySqlConnection(Bootstrap.Config.DatabaseConnectionString);

                        foreach (var sub in db.Query<PackageData>("SELECT `SubID`, `LastKnownName` FROM `Subs` WHERE `SubID` IN @Packages", new { changelist.Packages }))
                        {
                            Packages[sub.SubID] = sub.LastKnownName;
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
