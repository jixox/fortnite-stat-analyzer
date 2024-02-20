namespace StatAnalyzer
{
    using Azure;
    using Azure.Data.Tables;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using System;

    public class TableManager
    {
        /// <summary>
        /// Name of table containing all users' statistics.
        /// This is a public static string to allow UT's to switch to a test database.
        /// </summary>
        public static string UserStatsTableName = "UserStats";

        /// <summary>
        /// Table name containing all players' account information such as phone number and Epic account Id.
        /// This is a public static string to allow UT's to switch to a test database.
        /// </summary>
        public static string PlayerInfoTableName = "PlayerInfo";

        /// <summary>
        /// Provides Azure table level service operations.
        /// </summary>
        private TableServiceClient _tableServiceClient;

        /// <summary>
        /// Creates a TableManager object.
        /// </summary>
        public TableManager()
        {
            this._tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("STORAGE_CONN") ?? "None");
        }

        /// <summary>
        /// Grabs a list of all player entities from the Azure database.
        /// These entities represent all users who are opting-in to the stat analyzer service.
        /// </summary>
        /// <returns>List of PlayerInfoTableEntity objects read from the database.</returns>
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

        /// <summary>
        /// Reads all global stats pertaining to a user from the UserStats table.
        /// </summary>
        /// <param name="username">Username of player.</param>
        /// <param name="partition">Azure database partition. This is usually the Epic account Id pertaining to the given username.</param>
        /// <returns>List of UserStatsTableEntity objects read from the UserStats table.</returns>
        public List<UserStatsTableEntity> ReadUserStatsTableEntities(string username, string partition)
        {
            TableClient tableClient = this._tableServiceClient.GetTableClient(TableManager.UserStatsTableName);
            List<UserStatsTableEntity> collection = new List<UserStatsTableEntity>();

            // Filter the table query to return only rows that match the provided partition (Epic account Id).
            string filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");

            foreach (UserStatsTableEntity entity in tableClient.Query<UserStatsTableEntity>(filter))
            {
                collection.Add(entity);
            }

            return collection;
        }

        /// <summary>
        /// Writes the user's global stats to the UserStats table.
        /// Columns in this table consist of PartitionKey, RowKey, Timestamp, Username, SoloStats, DuoStats, TrioStats, and SquadStats.
        /// Each "<mode>Stats" column consists of strings in a JSON format.
        /// </summary>
        /// <param name="username">Player's username.</param>
        /// <param name="partition">Azure database partition name, usually the Epic account Id.</param>
        /// <param name="userStats">Global stats for the user.</param>
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

            // Create a new ITableRntity.
            // Azure DB will convert this object's properties into columns in the database. 
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

    /// <summary>
    /// Object representing the columns and rows that will populate the UserStats table.
    /// Note: The ETag property is not added as a column.
    /// </summary>
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
    }

    /// <summary>
    /// Object representing the columns and rows that will populate the PlayerInfo table.
    /// Note: The ETag property is not added as a column.
    /// </summary>
    public class PlayerInfoTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        ETag ITableEntity.ETag { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string EpicId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Cohort { get; set; } = string.Empty;
    }
}
