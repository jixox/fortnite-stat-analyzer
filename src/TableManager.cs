namespace StatAnalyzer
{
    using Azure;
    using Azure.Data.Tables;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using System;
    using System.Text;

    public class TableManager
    {
        public static string UserStatsTableName = "UserStats";

        public static string PlayerInfoTableName = "PlayerInfo";

        private TableServiceClient _tableServiceClient;

        public TableManager()
        {
            this._tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("STORAGE_CONN") ?? "None");
        }

        public List<PlayerInfoTableEntity> LoadPlayerInfoTableEntities()
        {
            TableClient tableClient = this._tableServiceClient.GetTableClient(TableManager.PlayerInfoTableName);
            List<PlayerInfoTableEntity> playerInfos = new List<PlayerInfoTableEntity>();

            foreach (PlayerInfoTableEntity entity in tableClient.Query<PlayerInfoTableEntity>())
            {
                playerInfos.Add(entity);
            }

            return playerInfos;
        }

        public List<UserStatsTableEntity> ReadUserStatsTableEntities(string username, string partition)
        {
            TableClient tableClient = this._tableServiceClient.GetTableClient(TableManager.UserStatsTableName);
            string filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
            List<UserStatsTableEntity> collection = new List<UserStatsTableEntity>();

            foreach (UserStatsTableEntity entity in tableClient.Query<UserStatsTableEntity>(filter))
            {
                collection.Add(entity);
            }

            return collection;
        }

        public void WriteUserStats(string username, string partition, JObject? userStats)
        {
            if (userStats == null)
            {
                return;
            }

            string soloStats = JsonConvert.SerializeObject(userStats.GetValue("solo"));
            string duoStats = JsonConvert.SerializeObject(userStats.GetValue("duo"));
            string trioStats = JsonConvert.SerializeObject(userStats.GetValue("trio"));
            string squadStats = JsonConvert.SerializeObject(userStats.GetValue("squad"));


            TableClient tableClient = this._tableServiceClient.GetTableClient(TableManager.UserStatsTableName);

            UserStatsTableEntity entity = new UserStatsTableEntity()
            {
                PartitionKey = partition,
                RowKey = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                Username = username,
                SoloStats = soloStats,
                DuoStats = duoStats,
                TrioStats = trioStats,
                SquadStats = squadStats
            };

            tableClient.AddEntity(entity);
        }
    }

    public class UserStatsTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        ETag ITableEntity.ETag { get; set; }
        public string Username { get; set; } = string.Empty;
        public string SoloStats { get; set; } = string.Empty;
        public string DuoStats { get; set; } = string.Empty;
        public string TrioStats { get; set; } = string.Empty;
        public string SquadStats { get; set; } = string.Empty;

        public string ToEssentialString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"PartitionKey={this.PartitionKey}");
            sb.AppendLine($"RowKey={this.RowKey}");
            sb.AppendLine($"Username={this.Username}");

            return sb.ToString();
        }
    }

    public class PlayerInfoTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        ETag ITableEntity.ETag { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string EpicId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
