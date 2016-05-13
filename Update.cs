using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System.IO;
using System.Reflection;

namespace ArenaHelper
{
    public static class Update
    {
        // Update code adapted from Hearthstone Collection Tracker Plugin

        // Plugin updates
        public const string releaseDownloadUrl = @"https://github.com/rembound/Arena-Helper";
        public const string latestReleaseRequestUrl = @"https://api.github.com/repos/rembound/Arena-Helper/releases/latest";

        // Data updates
        public const string DataVersionUrl = @"https://raw.githubusercontent.com/rembound/Arena-Helper/master/data/version.json";
        public const string HashListUrl = @"https://raw.githubusercontent.com/rembound/Arena-Helper/master/data/cardhashes.json";
        public const string TierListUrl = @"https://raw.githubusercontent.com/rembound/Arena-Helper/master/data/cardtier.json";

        // Updater
        public const string UpdaterFileName = "Updater.exe";
        public const string UpdaterFileNameNew = "Updater_new.exe";


        public const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";

        public static async Task<GithubRelease> GetLatestRelease()
        {
            try
            {
                string releaseStr;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);
                    releaseStr = await wc.DownloadStringTaskAsync(latestReleaseRequestUrl);
                }
                var releaseObj = JsonConvert.DeserializeObject<GithubRelease>(releaseStr);
                return (releaseObj == null ? null : releaseObj);
            }
            catch (Exception)
            {
            }
            return null;
        }

        public class GithubRelease
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }

            [JsonProperty("assets")]
            public List<Asset> Assets { get; set; }

            public class Asset
            {
                [JsonProperty("browser_download_url")]
                public string Url { get; set; }

                [JsonProperty("name")]
                public string Name { get; set; }
            }

            public Version GetVersion()
            {
                Version v;
                return (Version.TryParse(TagName, out v) ? v : null);
            }
        }


        // AHDataVersion
        public class AHDataVersion
        {
            public Version hashlist;
            public Version tierlist;

            public AHDataVersion(Version hashlist, Version tierlist)
            {
                this.hashlist = hashlist;
                this.tierlist = tierlist;
            }
        }

        public static async Task<AHDataVersion> GetDataVersion()
        {
            try
            {
                string versionStr;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);
                    versionStr = await wc.DownloadStringTaskAsync(DataVersionUrl);
                }
                return JsonConvert.DeserializeObject<AHDataVersion>(versionStr, new VersionConverter());
            }
            catch (Exception)
            {
            }
            return null;
        }

        // Download a string from a URL
        public static async Task<string> DownloadString(string url)
        {
            try
            {
                string str;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);
                    str = await wc.DownloadStringTaskAsync(url);
                }
                return str;
            }
            catch (Exception)
            {
            }
            return null;
        }

        // Start auto updater
        public static async void AutoUpdate(GithubRelease release)
        {
            try
            {
                if (release.Assets.Count > 0)
                {
                    string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    string updater = Path.Combine(assemblylocation, UpdaterFileName);
                    Process.Start(updater, string.Format("{0} {1}", Process.GetCurrentProcess().Id, release.Assets[0].Url));
                    Hearthstone_Deck_Tracker.API.Core.MainWindow.Close();
                    System.Windows.Application.Current.Shutdown();
                }
            }
            catch(Exception e)
            {
                // Manual update
                Log.Info("AutoUpdate error: " + e.Message);
                Process.Start(releaseDownloadUrl);
            }
        }

        // Clean up auto updater
        public static void CleanAutoUpdate()
        {
            try
            {
                string assemblylocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string updater = Path.Combine(assemblylocation, UpdaterFileName);
                string updaternew = Path.Combine(assemblylocation, UpdaterFileNameNew);


                if (File.Exists(updaternew))
                {
                    if (File.Exists(updater))
                        File.Delete(updater);
                    File.Move(updaternew, updater);
                }
            }
            catch (Exception e)
            {
                Log.Info("Error updating Arena Helper updater");
            }
        }
    }
}
