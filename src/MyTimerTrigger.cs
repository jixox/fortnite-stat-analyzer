namespace Spca.Function
{
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;
    using StatAnalyzer;
    using System;
    using Twilio;
    using Twilio.Rest.Api.V2010.Account;

    public class MyTimerTrigger
    {
        /// <summary>
        /// Time the Run method was triggered. Set to the current UTC time by default.
        /// </summary>
        public static DateTime RunTime = DateTime.UtcNow;

        /// <summary>
        /// Text message levels depending on scenario.
        /// </summary>
        public enum TextMsgLevel
        {
            OK,
            Error,
            Debug
        }

        /// <summary>
        /// ID associated with Twilio account (shhh it's a secret).
        /// </summary>
        private static readonly string TWILIO_SID = Environment.GetEnvironmentVariable(nameof(TWILIO_SID)) ?? "None";

        /// <summary>
        /// Token associated with Twilio account (shhh it's a secret).
        /// </summary>
        private static readonly string TWILIO_TKN = Environment.GetEnvironmentVariable(nameof(TWILIO_TKN)) ?? "None";

        /// <summary>
        /// Twilio phone number used to send messages (shhh it's a secret).
        /// </summary>
        private static readonly string TWILIO_PHONE = Environment.GetEnvironmentVariable(nameof(TWILIO_PHONE)) ?? "None";

        /// <summary>
        /// Logger object.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Object containing read and write DB methods.
        /// </summary>
        private TableManager _tableManager;

        /// <summary>
        /// Object providing access to Fortnite API stats.
        /// </summary>
        private FortniteClient _fortniteClient;

        /// <summary>
        /// Constructs a MyTimerTrigger object.
        /// </summary>
        /// <param name="loggerFactory">Logger factory from which an ILogger instance is created.</param>
        public MyTimerTrigger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MyTimerTrigger>();
            _fortniteClient = new FortniteClient();
            _tableManager = new TableManager();
        }

        /// <summary>
        /// Entry point into project's logic. Triggered every morning at 1300 UTC.
        /// </summary>
        /// <param name="myTimer">NCRONTAB timer object.</param>
        [Function("MyTimerTrigger")]
        public void Run([TimerTrigger("0 0 13 * * *")] TimerInfo myTimer)
        {
            // Update the timer's latest run time.
            MyTimerTrigger.RunTime = DateTime.UtcNow;

            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            // Step into the meat of the logic.
            this.RunInternal(true).Wait();
        }

        /// <summary>
        /// Retrieves Fortnite stats, calculates deltas for each player, and sends text messages.
        /// </summary>
        /// <param name="shouldSendText">Whether or not to send a text message.</param>
        /// <returns>Task representing operations.</returns>
        public async Task RunInternal(bool shouldSendText)
        {
            // Initialize the Twilio client.
            // I'm assuming the MyTimerTrigger constructor gets called only once per service restart, so I'd like this client to not remain undisposed for long durations,
            // hence why I'm putting it here. It probably doesn't matter but oh well.
            TwilioClient.Init(MyTimerTrigger.TWILIO_SID, MyTimerTrigger.TWILIO_TKN);

            // Load player account information from database.
            List<PlayerInfoTableEntity> playerInfos = this._tableManager.LoadPlayerInfoTableEntities();

            Dictionary<string, string> cohortMessages = new Dictionary<string, string>();
            int lookback;
            string cohortIntroMessage = "";
            TextMsgLevel txtMsgLevel = TextMsgLevel.OK;

            // If the day is Sunday, provide a summary of stats pertaining to the previous week rather than the usual lookback period.
            if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
            {
                lookback = 7;
                cohortIntroMessage = "Weekly summary!\n";
            }
            else
            {
                lookback = 1;
            }

            cohortIntroMessage = string.Join("", cohortIntroMessage, "Stats from the past {0} day(s):\n\n{1}");

            try
            {
                foreach (PlayerInfoTableEntity playerInfo in playerInfos)
                {
                    // Retrieve global stats from Fortnite IO's API.
                    JObject? globalStats = await this._fortniteClient.GetUserStats(playerInfo.EpicId);
                    
                    // Write stats to the database
                    this._tableManager.WriteUserStats(playerInfo.Username, playerInfo.EpicId, globalStats);
                    
                    // Read all of the user's records in the database into a list of UserStatsTableEntity objects.
                    List<UserStatsTableEntity> userStatsTableEntities = this._tableManager.ReadUserStatsTableEntities(playerInfo.EpicId);

                    // Bundle all of the user's stats into a single UserStatsCollection object.
                    UserStatsCollection collection = new UserStatsCollection(playerInfo.Username, userStatsTableEntities);

                    // Retrieve the user's time-bounded aggregate stat bundle in string form.
                    string timeBoundedUserStats = collection.GetTimeBoundedUserStatsString(lookback: lookback) + "\n";

                    // Each players cohort(s) are in the form of "c1,c2,c3" (etc.) in the Azure database.
                    string[] cohorts = playerInfo.Cohort.Split(',');

                    // Append the users' time-bounded stats to each of their cohorts' messages.
                    // Think of a "cohort" as a group of friends. Each group of friends receives all stats of players in their cohort.
                    // A player can have multiple cohorts.
                    foreach (string cohort in cohorts)
                    {
                        if (cohortMessages.TryGetValue(cohort, out _))
                        {
                            // Append the current user's stats to their pre-existing cohort message.
                            cohortMessages[cohort] += timeBoundedUserStats;
                        }
                        else
                        {
                            // Begin this cohort's message with the first user's stats.
                            cohortMessages.Add(cohort, string.Format(cohortIntroMessage, lookback, timeBoundedUserStats));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // If there is even one exception thrown, regardless of the successes of previous players, do not retry and do not provide anyone with stats.
                // NOTE: There is room for optimization here, just too lazy to write more code now.
                if (ex is TimeoutException && ex.Message.Contains(nameof(FortniteClient.GetUserStats)))
                {
                    this._logger.LogError($"{nameof(RunInternal)}: TimeoutException occurred. {ex.Message}");
                    txtMsgLevel = TextMsgLevel.Error;
                }
                else
                {
                    // Unhandled exception has occurred, log and re-throw.
                    this._logger.LogError($"{nameof(RunInternal)}: Unhandled exception has occurred! {ex.Message}");
                    throw;
                }
            }

            if (shouldSendText)
            {
                // Send texts to each player.
                // If a player is in multiple cohorts, send one per each.
                foreach (PlayerInfoTableEntity playerInfo in playerInfos)
                {
                    string[] cohorts = playerInfo.Cohort.Split(',');

                    foreach (string cohort in cohorts)
                    {
                        // Send texts to players based on whether or not an error has occurred during the stat retrieval/calculation process.
                        switch (txtMsgLevel)
                        {
                            case TextMsgLevel.OK:
                                this.SendText(playerInfo.Phone, cohortMessages[cohort]);
                                break;

                            case TextMsgLevel.Error:
                                this.SendErrorText(playerInfo.Phone);
                                break;

                            case TextMsgLevel.Debug:
                                // Fall-through case for now until further implementation.

                            default:
                                break;
                        }
                    }
                }
            }

            // Sprinkle in some extra logging for debugging purposes.
            foreach (KeyValuePair<string, string> cohortMessage in cohortMessages)
            {
                this._logger.LogInformation($"Cohort {cohortMessage.Key} message:\n\n{cohortMessage.Value}\n===============================\n");
            }
        }

        /// <summary>
        /// Send a text message via the Twilio platform.
        /// </summary>
        /// <param name="number">Recipient of message.</param>
        /// <param name="message">Message to send.</param>
        public void SendText(string number, string message)
        {
            MessageResource.Create(
                body: message,
                from: new Twilio.Types.PhoneNumber(MyTimerTrigger.TWILIO_PHONE),
                to: new Twilio.Types.PhoneNumber(number)
            );
        }

        /// <summary>
        /// Send an error text message via the Twilio platform in the case of a TimeoutException retrieving.
        /// </summary>
        /// <param name="number">Recipient of message.</param>
        public void SendErrorText(string number)
        {
            MessageResource.Create(
                body: "Uh oh! The Fortnite API IO service is having some issues providing global stats. No stats for today :/",
                from: new Twilio.Types.PhoneNumber(MyTimerTrigger.TWILIO_PHONE),
                to: new Twilio.Types.PhoneNumber(number)
            );
        }
    }
}
