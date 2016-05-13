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
        public readonly Dictionary<uint, Dictionary<string, string>> Apps;

        [JsonProperty]
        public readonly Dictionary<uint, Dictionary<string, string>> Packages;

        public ChangelistEvent(SteamApps.PICSChangesCallback callback)
            : base("Changelist")
        {
            ChangeNumber = callback.CurrentChangeNumber;

            if (callback.AppChanges.Any())
            {
                Apps = new Dictionary<uint, Dictionary<string, string>>();
                foreach(KeyValuePair<uint, SteamApps.PICSChangesCallback.PICSChangeData> entry in callback.AppChanges)
                {
                    Dictionary<string, string> Properties = new Dictionary<string, string>();
                    Properties.Add("Name", "Unknown App " + entry.Key);
                    Properties.Add("ChangeNumber", entry.Value.ChangeNumber.ToString());
                    Apps.Add(entry.Key, Properties);
                }

                if (Bootstrap.DatabaseConnectionString != null)
                {
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

                                Apps[reader.GetUInt32(0)]["Name"] = name;
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
                Apps = new Dictionary<uint, Dictionary<string, string>>();
            }

            if (callback.PackageChanges.Any())
            {
                Packages = new Dictionary<uint, Dictionary<string, string>>();
                foreach(KeyValuePair<uint, SteamApps.PICSChangesCallback.PICSChangeData> entry in callback.PackageChanges)
                {
                    Dictionary<string, string> Properties = new Dictionary<string, string>();
                    Properties.Add("Name", "Unknown Package " + entry.Key);
                    Properties.Add("ChangeNumber", entry.Value.ChangeNumber.ToString());
                    Packages.Add(entry.Key, Properties);
                }

                if (Bootstrap.DatabaseConnectionString != null)
                {
                    try
                    {
                        var subs = string.Join(",", callback.PackageChanges.Keys);

                        using (var reader = MySqlHelper.ExecuteReader(Bootstrap.DatabaseConnectionString, "SELECT `SubID`, `LastKnownName` FROM `Subs` WHERE `SubID` IN(" + subs + ")"))
                        {
                            while (reader.Read())
                            {
                                Packages[reader.GetUInt32(0)]["Name"] = reader.GetString(1);
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
                Packages = new Dictionary<uint, Dictionary<string, string>>();
            }
        }
    }
}
