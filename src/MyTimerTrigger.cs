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
        public static DateTime RunTime = DateTime.UtcNow;

        private static readonly string TWILIO_SID = Environment.GetEnvironmentVariable(nameof(TWILIO_SID)) ?? "None";

        private static readonly string TWILIO_TKN = Environment.GetEnvironmentVariable(nameof(TWILIO_TKN)) ?? "None";

        private static readonly string TWILIO_PHONE = Environment.GetEnvironmentVariable(nameof(TWILIO_PHONE)) ?? "None";

        private readonly ILogger _logger;

        private TableManager _tableManager;

        private FortniteClient _fortniteClient;

        public MyTimerTrigger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MyTimerTrigger>();
            _fortniteClient = new FortniteClient();
            _tableManager = new TableManager();
        }

        [Function("MyTimerTrigger")]
        public void Run([TimerTrigger("0 0 9 * * *")] TimerInfo myTimer)
        {
            MyTimerTrigger.RunTime = DateTime.UtcNow;

            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            this.runInternal().Wait();
        }

        private async Task runInternal()
        {
            List<PlayerInfoTableEntity> playerInfos = this._tableManager.LoadPlayerInfoTableEntities();
            string message = "";

            foreach (PlayerInfoTableEntity playerInfo in playerInfos)
            {
                JObject? globalStats = await this._fortniteClient.GetUserStats(playerInfo.EpicId);
                this._tableManager.WriteUserStats(playerInfo.Username, playerInfo.EpicId, globalStats);
                List<UserStatsTableEntity> userStatsTableEntities = this._tableManager.ReadUserStatsTableEntities(playerInfo.Username, playerInfo.EpicId);
                UserStatsCollection collection = new UserStatsCollection(playerInfo.Username, userStatsTableEntities);
                message += collection.GetTimeBoundedUserStatsString(lookback: 1) + "\n";
            }

            foreach (PlayerInfoTableEntity playerInfo in playerInfos)
            {
                this.SendText(playerInfo.Phone, message);
            }

            this._logger.LogInformation($"Text message being sent:\n{message}");
        }

        public void SendText(string number, string message)
        {
            TwilioClient.Init(MyTimerTrigger.TWILIO_SID, MyTimerTrigger.TWILIO_TKN);

            MessageResource.Create(
                body: $"[Beta Testing]\n\n{message}",
                from: new Twilio.Types.PhoneNumber(MyTimerTrigger.TWILIO_PHONE),
                to: new Twilio.Types.PhoneNumber(number)
            );
        }
    }
}
