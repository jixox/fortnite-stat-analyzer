namespace StatAnalyzer
{
    using Newtonsoft.Json.Linq;
    using Spca.Function;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class UserStatsCollection
    {
        private List<UserStats> _userStatsList;
        private string _username;

        public UserStatsCollection(string username, List<UserStatsTableEntity> userStatsTableEntities)
        {
            this._userStatsList = new List<UserStats>();
            this._username = username;

            foreach (UserStatsTableEntity entity in userStatsTableEntities)
            {
                this._userStatsList.Add(new UserStats(entity));
            }

            this._userStatsList.Sort((x, y) =>
            {
                return x.Timestamp > y.Timestamp ? 1
                        : x.Timestamp < y.Timestamp ? -1
                        : 0;
            });
        }

        public string GetTimeBoundedUserStatsString(int lookback)
        {
            int startIndex = 0;
            int endIndex = this._userStatsList.Count - 1;
            DateTime maxLookbackTime = MyTimerTrigger.RunTime.AddDays(-1 * lookback);

            foreach (UserStats userStats in this._userStatsList)
            {
                if (userStats.Timestamp >= maxLookbackTime)
                {
                    break;
                }

                startIndex++;
            }

            int playersBagged = -1;
            int matchesPlayed = -1;
            int minutesPlayed = -1;
            int top25 = -1;
            int top10 = -1;
            int top5 = -1;
            int top3 = -1;
            int top1 = -1;
            double kd = -1.0;
            double km = -1.0;

            StringBuilder sb = new StringBuilder();

            if (startIndex != endIndex && endIndex != -1)
            {
                ModeStats aggregateMode0 = this._userStatsList[startIndex].AggregateModeStats;
                ModeStats aggregateMode1 = this._userStatsList[endIndex].AggregateModeStats;
                
                playersBagged = aggregateMode1.PlayersBagged - aggregateMode0.PlayersBagged;
                matchesPlayed = aggregateMode1.MatchesPlayed - aggregateMode0.MatchesPlayed;
                minutesPlayed = aggregateMode1.MinutesPlayed - aggregateMode0.MinutesPlayed;
                km = playersBagged / (double)minutesPlayed;
                top25 = aggregateMode1.Top25 - aggregateMode0.Top25;
                top10 = aggregateMode1.Top10 - aggregateMode0.Top10;
                top5 = aggregateMode1.Top5 - aggregateMode0.Top5;
                top3 = aggregateMode1.Top3 - aggregateMode0.Top3;
                top1 = aggregateMode1.Top1 - aggregateMode0.Top1;

                if (matchesPlayed - top1 > 0)
                {
                    kd = playersBagged / (double)(matchesPlayed - top1);
                }
            }

            string kdString = matchesPlayed == top1 && matchesPlayed > 0 && playersBagged > 0 ? "\u221E" : Math.Round(kd, 2).ToString();

            sb.AppendLine($"Username: {this._username}");
            sb.AppendLine($"Players Bagged: {playersBagged}");
            sb.AppendLine($"K/D: {kdString}");
            sb.AppendLine($"K/Min: {Math.Round(km, 2)}");
            sb.AppendLine($"Matches Played: {matchesPlayed}");
            sb.AppendLine($"Minutes Played: {minutesPlayed} ({minutesPlayed / 60} hours)");
            sb.AppendLine($"Top 1: {top1}");
            sb.AppendLine($"Top 3: {top3}");
            sb.AppendLine($"Top 5: {top5}");
            sb.AppendLine($"Top 10: {top10}");
            sb.AppendLine($"Top 25: {top25}");

            return sb.ToString();
        }

        public void PrintAggregateModeStats()
        {
            foreach (UserStats userStat in _userStatsList)
            {
                Console.WriteLine("========================");
                Console.WriteLine(userStat.AggregateModeStats.ToString());
            }
        }

        private class UserStats
        {
            public DateTimeOffset? Timestamp;
            public ModeStats AggregateModeStats;
            public ModeStats[] ModeStats;

            public UserStats(UserStatsTableEntity userStatsTableEntity)
            {
                this.Timestamp = userStatsTableEntity.Timestamp;
                this.ModeStats = new ModeStats[4];
                this.ModeStats[0] = new ModeStats("solo", JObject.Parse(userStatsTableEntity.SoloStats));
                this.ModeStats[1] = new ModeStats("duo", JObject.Parse(userStatsTableEntity.DuoStats));
                this.ModeStats[2] = new ModeStats("trio", JObject.Parse(userStatsTableEntity.TrioStats));
                this.ModeStats[3] = new ModeStats("squad", JObject.Parse(userStatsTableEntity.SquadStats));
                this.AggregateModeStats = this.getAggregateModeStats();
            }

            private ModeStats getAggregateModeStats()
            {
                ModeStats aggregateModeStats = new ModeStats("aggregate")
                {
                    LastModified = DateTime.Now,
                    PlayersBagged = this.getSumOfGivenIntStat("PlayersBagged"),
                    MatchesPlayed = this.getSumOfGivenIntStat("MatchesPlayed"),
                    MinutesPlayed = this.getSumOfGivenIntStat("MinutesPlayed"),
                    Top1 = this.getSumOfGivenIntStat("Top1"),
                    Top3 = this.getSumOfGivenIntStat("Top3"),
                    Top5 = this.getSumOfGivenIntStat("Top5"),
                    Top6 = this.getSumOfGivenIntStat("Top6"),
                    Top10 = this.getSumOfGivenIntStat("Top10"),
                    Top12 = this.getSumOfGivenIntStat("Top12"),
                    Top25 = this.getSumOfGivenIntStat("Top25"),
                };

                return aggregateModeStats;
            }

            private int getSumOfGivenIntStat(string propertyName)
            {
                int sum = 0;

                foreach (ModeStats modeStat in this.ModeStats)
                {
                    sum += (int)(modeStat.GetType().GetProperty(propertyName).GetValue(modeStat));
                }

                return sum;
            }
        }

        public class ModeStats
        {
            public string Mode { get; set; }

            public DateTime LastModified { get; set; }

            public int PlayersBagged { get; set; }

            public int MatchesPlayed { get; set; }

            public int MinutesPlayed { get; set; }

            public int Top1 { get; set; }

            public int Top3 { get; set; }

            public int Top5 { get; set; }

            public int Top6 { get; set; }

            public int Top10 { get; set; }

            public int Top12 { get; set; }

            public int Top25 { get; set; }

            public ModeStats(string mode, JObject? stats)
            {
                long lastModifiedUnix = long.Parse(stats.GetValue("lastmodified").ToString());

                this.Mode = mode;
                this.LastModified = DateTimeOffset.FromUnixTimeSeconds(lastModifiedUnix).DateTime;
                this.PlayersBagged = int.Parse(stats.GetValue("kills").ToString());
                this.MatchesPlayed = int.Parse(stats.GetValue("matchesplayed").ToString());
                this.MinutesPlayed = int.Parse(stats.GetValue("minutesplayed").ToString());

                int top25 = int.Parse(stats.GetValue("placetop25").ToString());
                int top12 = int.Parse(stats.GetValue("placetop12").ToString());
                int top10 = int.Parse(stats.GetValue("placetop10").ToString());
                int top6 = int.Parse(stats.GetValue("placetop6").ToString());
                int top5 = int.Parse(stats.GetValue("placetop5").ToString());
                int top3 = int.Parse(stats.GetValue("placetop3").ToString());
                int top1 = int.Parse(stats.GetValue("placetop1").ToString());

                this.Top1 = top1;
                this.Top3 = top3 + this.Top1;
                this.Top5 = top5 + this.Top3;
                this.Top6 = top6 + this.Top5;
                this.Top10 = top10 + this.Top6;
                this.Top12 = top12 + this.Top10;
                this.Top25 = top25 + this.Top12;
            }

            public ModeStats(string mode)
            {
                if (mode != "aggregate")
                {
                    throw new InvalidOperationException();
                }

                this.Mode = mode;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"Mode: {this.Mode}");
                sb.AppendLine($"Last Modified: {this.LastModified}");
                sb.AppendLine($"Players Bagged: {this.PlayersBagged}");
                sb.AppendLine($"Matches Played: {this.MatchesPlayed}");
                sb.AppendLine($"Minutes Played: {this.MinutesPlayed}");

                return sb.ToString();
            }
        }
    }
}
