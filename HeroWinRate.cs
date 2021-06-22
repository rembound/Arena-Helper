using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;

namespace ArenaHelper
{
    /// <summary>
    /// Provides hero win rate information in arena mode. Information is requesting from HSReplay
    /// </summary>
    public class HeroWinRate
    {
        private const string HSReplyPerformanceSummaryUrl = "https://hsreplay.net/api/v1/analytics/query/player_class_performance_summary";
        private const int ArenaGameType = 3;

        private Dictionary<string, double> heroWinRateDictionary;

        /// <summary>
        /// Search for win rate by hero class
        /// </summary>
        /// <param name="heroClass">Hero class</param>
        /// <returns>Win rate of specified hero</returns>
        public string GetWinRateAsString(string heroClass)
        {
            if (heroWinRateDictionary == null)
            {
                FillWinRateDictionary();
            }

            return string.Format("{0:0.0}%", heroWinRateDictionary[heroClass.ToLower()]);
        }

        private void FillWinRateDictionary()
        {
            WebRequest request = WebRequest.Create(HSReplyPerformanceSummaryUrl);

            using (WebResponse response = request.GetResponse())
            {
                if ((response as HttpWebResponse).StatusCode != HttpStatusCode.OK)
                {
                    throw new System.Exception("Can't get information about hero win rate");
                }

                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = reader.ReadToEnd();
                    dynamic responseObject = JObject.Parse(responseBody);

                    heroWinRateDictionary = new Dictionary<string, double>();
                    foreach (var heroWinRateDataPair in responseObject.series.data)
                    {
                        JArray winRateData = heroWinRateDataPair.Value;
                        dynamic arenaWinRateData = winRateData.First(data => (data as dynamic).game_type == ArenaGameType);
                        JValue winRate = arenaWinRateData.win_rate;
                        heroWinRateDictionary[heroWinRateDataPair.Name.ToLower()] = (double)winRate.Value;
                    }
                }
            }
        }
    }
}
