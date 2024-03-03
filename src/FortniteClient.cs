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
            CancellationTokenSource src = new CancellationTokenSource();
            CancellationToken token = src.Token;
            string uri = $"https://fortniteapi.io/v1/stats?account={accountId}";
            JObject? globalStatsJson = null;

            // Spool-up a timeout task that invokes the cancellation token after 30 seconds. If the Fortnite API is not returning after 30 seconds,
            // it is likely not going to succeed.
            Task timeoutTask = Task.Run(async () => await Task.Delay(30000)).ContinueWith((antecedent) => src.Cancel());

            // Spool-up another task to run the API call.
            Task apiTask = Task.Run(async () =>
            {
                using (HttpResponseMessage response = await this._httpClient.GetAsync(uri, token))
                {
                    string stats = await response.Content.ReadAsStringAsync();
                    JObject statsJson = JObject.Parse(stats);

                    globalStatsJson = statsJson.GetValue("global_stats") as JObject;
                }
            });

            // If the timeout task is the winner of the race, throw a TimeoutException.
            // Otherwise, return the global stats from the API.
            if (await Task.WhenAny(apiTask, timeoutTask) == timeoutTask)
            {
                throw new TimeoutException($"{nameof(GetUserStats)}: 30 second timeout hit when retrieving user stats from the Fortnite API.");
            }
            else
            {
                return globalStatsJson;
            }
        }
    }
}
