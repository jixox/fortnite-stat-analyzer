namespace StatAnalyzer
{
    using Newtonsoft.Json.Linq;
    using System.Threading.Tasks;

    public class FortniteClient
    {
        private HttpClient _httpClient;

        public FortniteClient()
        {
            this._httpClient = new HttpClient();
            this._httpClient.DefaultRequestHeaders.Add("Authorization", Environment.GetEnvironmentVariable("FORT_AUTH"));
        }

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
