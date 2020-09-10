using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using Dapper;

namespace SteamWebPipes.Events
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

        public uint ChangeNumber { get; private set; }
        public Dictionary<string, string> Apps { get; private set; }
        public Dictionary<string, string> Packages { get; private set; }

        public ChangelistEvent(SteamChangelist changelist)
            : base("Changelist")
        {
            ChangeNumber = changelist.ChangeNumber;

            if (changelist.Apps.Any())
            {
                var apps = changelist.Apps.ToDictionary(x => x, x => "Unknown App " + x);

                if (Bootstrap.Config.DatabaseConnectionString != null)
                {
                    try
                    {
                        using var db = new MySqlConnection(Bootstrap.Config.DatabaseConnectionString);

                        foreach (var app in db.Query<AppData>("SELECT `AppID`, `Name`, `LastKnownName` FROM `Apps` WHERE `AppID` IN @Apps", new { changelist.Apps }))
                        {
                            if (!string.IsNullOrEmpty(app.LastKnownName) && app.Name != app.LastKnownName)
                            {
                                apps[app.AppID] = $"{app.Name} ({app.LastKnownName})";
                            }
                            else
                            {
                                apps[app.AppID] = app.Name;
                            }
                        }
                    }
                    catch (MySqlException e)
                    {
                        Bootstrap.Log("{0}", e.Message);
                    }
                }

                Apps = apps.ToDictionary(x => x.Key.ToString(), x => x.Value);
            }
            else
            {
                Apps = new Dictionary<string, string>();
            }

            if (changelist.Packages.Any())
            {
                var packages = changelist.Packages.ToDictionary(x => x, x => "Unknown Package " + x);

                if (Bootstrap.Config.DatabaseConnectionString != null)
                {
                    try
                    {
                        using var db = new MySqlConnection(Bootstrap.Config.DatabaseConnectionString);

                        foreach (var sub in db.Query<PackageData>("SELECT `SubID`, `LastKnownName` FROM `Subs` WHERE `SubID` IN @Packages", new { changelist.Packages }))
                        {
                            packages[sub.SubID] = sub.LastKnownName;
                        }
                    }
                    catch (MySqlException e)
                    {
                        Bootstrap.Log("{0}", e.Message);
                    }
                }

                Packages = packages.ToDictionary(x => x.Key.ToString(), x => x.Value);
            }
            else
            {
                Packages = new Dictionary<string, string>();
            }
        }
    }
}
