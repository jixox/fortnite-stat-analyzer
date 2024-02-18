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
        /// Entry point into project's logic. Triggered every morning at 0900 UTC.
        /// </summary>
        /// <param name="myTimer">NCRONTAB timer object.</param>
        [Function("MyTimerTrigger")]
        public void Run([TimerTrigger("0 0 9 * * *")] TimerInfo myTimer)
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
            string message = "";

            foreach (PlayerInfoTableEntity playerInfo in playerInfos)
            {
                // Retrieve global stats from Fortnite IO's API.
                JObject? globalStats = await this._fortniteClient.GetUserStats(playerInfo.EpicId);

                // Write stats to the database.
                this._tableManager.WriteUserStats(playerInfo.Username, playerInfo.EpicId, globalStats);

                // Read all of the user's records in the database into a list of UserStatsTableEntity objects.
                List<UserStatsTableEntity> userStatsTableEntities = this._tableManager.ReadUserStatsTableEntities(playerInfo.Username, playerInfo.EpicId);
                
                // Bundle all of the user's stats into a single UserStatsCollection object.
                UserStatsCollection collection = new UserStatsCollection(playerInfo.Username, userStatsTableEntities);

                // Append the user's aggregated stats to the text message.
                message += collection.GetTimeBoundedUserStatsString(lookback: 1) + "\n";
            }

            if (shouldSendText)
            {
                // Send texts to each player.
                foreach (PlayerInfoTableEntity playerInfo in playerInfos)
                {
                    this.SendText(playerInfo.Phone, message);
                }
            }

            this._logger.LogInformation($"Text message being sent:\n{message}");
        }

        /// <summary>
        /// Send a text message via the Twilio platform.
        /// </summary>
        /// <param name="number">Recipient of message.</param>
        /// <param name="message">Message to send.</param>
        public void SendText(string number, string message)
        {
            MessageResource.Create(
                body: $"[Beta Testing]\n\n{message}",
                from: new Twilio.Types.PhoneNumber(MyTimerTrigger.TWILIO_PHONE),
                to: new Twilio.Types.PhoneNumber(number)
            );
        }
    }
}
