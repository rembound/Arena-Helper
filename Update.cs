using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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


        public const string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";

        public static async Task<Version> GetLatestVersion()
        {
            try
            {
                string versionStr;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", userAgent);
                    versionStr = await wc.DownloadStringTaskAsync(latestReleaseRequestUrl);
                }
                var versionObj = JsonConvert.DeserializeObject<GithubRelease>(versionStr);
                return versionObj == null ? null : new Version(versionObj.TagName);
            }
            catch (Exception)
            {
            }
            return null;
        }

        private class GithubRelease
        {
            [JsonProperty("tag_name")]
            public string TagName { get; set; }
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
    }
}
