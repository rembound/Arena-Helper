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
        public static async Task<bool> AutoUpdate(GithubRelease release)
        {
            bool status = true;
            try
            {
                if (release.Assets.Count > 0)
                {
                    // Update and install
                    status = await AutoUpdater.Update(release.Assets[0].Url);
                }
            }
            catch(Exception e)
            {
                status = false;

                Log.Info("AutoUpdate error: " + e.Message);
            }

            return status;
        }

        // Clean up auto updater
        // TODO: When coming from 0.8 and skipping a version, how to add new files (they are removed if not synced)
        public static void CleanAutoUpdate()
        {

        }
    }
}
