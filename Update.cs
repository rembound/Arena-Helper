using Newtonsoft.Json;
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
        // Code from: Hearthstone Collection Tracker Plugin

        public const string releaseDownloadUrl = @"https://github.com/rembound/Arena-Helper/releases/latest";
        public const string latestReleaseRequestUrl = @"https://api.github.com/repos/rembound/Arena-Helper/releases/latest";

        public static async Task<Version> GetLatestVersion()
        {
            try
            {
                string versionStr;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
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
    }
}
