namespace StatAnalyzer
{
    using Newtonsoft.Json.Linq;
    using System.Threading.Tasks;

    public class FortniteClient
    {
        /// <summary>
        /// Client providing HTTP communication services.
        /// </summary>
        private HttpClient _httpClient;

        /// <summary>
        /// Constructs a FortniteClient object.
        /// </summary>
        public FortniteClient()
        {
            this._httpClient = new HttpClient();

            // Add the Fortnite IO authorization token as a header.
            this._httpClient.DefaultRequestHeaders.Add("Authorization", Environment.GetEnvironmentVariable("FORT_AUTH"));
        }

        /// <summary>
        /// Retrieves the user's global stats.
        /// </summary>
        /// <param name="accountId">Epic account Id.</param>
        /// <returns>A user's stats in JObect form.</returns>
        public async Task<JObject?> GetUserStats(string accountId)
        {
            string uri = $"https://fortniteapi.io/v1/stats?account={accountId}";
            JObject? globalStatsJson;

            using (HttpResponseMessage response = await this._httpClient.GetAsync(uri))
            {
                string stats = await response.Content.ReadAsStringAsync();
                JObject statsJson = JObject.Parse(stats);

                globalStatsJson = statsJson.GetValue("global_stats") as JObject;
            }

            return globalStatsJson;
        }
    }
}
