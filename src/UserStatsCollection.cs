﻿namespace StatAnalyzer
{
    using Newtonsoft.Json.Linq;
    using Spca.Function;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class UserStatsCollection
    {
        /// <summary>
        /// List of UserStats serving as the backbone of this object.
        /// </summary>
        private List<UserStats> _userStatsList;

        /// <summary>
        /// Username of to whom these stats belong.
        /// </summary>
        private string _username;

        /// <summary>
        /// Constructs a UserStatsCollection object.
        /// </summary>
        /// <param name="username">Username of player.</param>
        /// <param name="userStatsTableEntities">Deserialized database UserStats rows.</param>
        public UserStatsCollection(string username, List<UserStatsTableEntity> userStatsTableEntities)
        {
            this._userStatsList = new List<UserStats>();
            this._username = username;

            // Convert each UserStatsTableEntity object into an object holding condensed and organized essential information and append to the backing list.
            foreach (UserStatsTableEntity entity in userStatsTableEntities)
            {
                this._userStatsList.Add(new UserStats(entity));
            }

            // Sort the list in chronological order.
            this._userStatsList.Sort((x, y) =>
            {
                return x.Timestamp > y.Timestamp ? 1
                        : x.Timestamp < y.Timestamp ? -1
                        : 0;
            });
        }

        /// <summary>
        /// Retrieve the collection's aggregate stats looking back a certain amount of time.
        /// Stats are presented based on the delta between a user's total global stat entries.
        /// </summary>
        /// <param name="lookback">Number of days worth of stats to consider.</param>
        /// <returns></returns>
        public string GetTimeBoundedUserStatsString(int lookback)
        {
            int startIndex = 0;
            int endIndex = this._userStatsList.Count - 1;
            DateTime maxLookbackTime = MyTimerTrigger.RunTime.AddDays(-1 * lookback);

            foreach (UserStats userStats in this._userStatsList)
            {
                if (userStats.Timestamp >= maxLookbackTime)
                {
                    // Break out of loop as soon as the first record within the lookback time is found.
                    break;
                }

                startIndex++;
            }

            // Initialize stats as negative. These should only be negative when a user has no previous table entries.
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

            // Calculate stats when there's more than one entry.
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

            // When a user has only top 1 placements and at least 1 player bagged, display an inifinity sign to make them feel special.
            string kdString = matchesPlayed == top1 && matchesPlayed > 0 && playersBagged > 0 ? "\u221E" : Math.Round(kd, 2).ToString();

            // Append all of the stats to the string builder.
            sb.AppendLine($"Username: {this._username}");
            sb.AppendLine($"Players Bagged: {playersBagged}");
            sb.AppendLine($"K/D: {kdString}");
            sb.AppendLine($"K/Min: {Math.Round(km, 2)}");
            sb.AppendLine($"Matches Played: {matchesPlayed}");
            sb.AppendLine($"Minutes Played: {minutesPlayed} ({Math.Abs(Math.Round(minutesPlayed / (double)60, 1))} hours)");
            sb.AppendLine($"Top 1: {top1}");
            sb.AppendLine($"Top 3: {top3}");
            sb.AppendLine($"Top 5: {top5}");
            sb.AppendLine($"Top 10: {top10}");
            sb.AppendLine($"Top 25: {top25}");

            return sb.ToString();
        }

        /// <summary>
        /// Class representing a user's statistics in an organized fashion.
        /// Each game mode (solo, duo, etc.) is broken up into an array of ModeStats.
        /// </summary>
        private class UserStats
        {
            /// <summary>
            /// Timestamp of the corresponding UserStats table entry.
            /// </summary>
            public DateTimeOffset? Timestamp;

            /// <summary>
            /// Aggregate stats across all game modes.
            /// </summary>
            public ModeStats AggregateModeStats;

            /// <summary>
            /// Stats pertaining to each game mode.
            /// </summary>
            public ModeStats[] ModeStats;

            /// <summary>
            /// Creates a UserStats object.
            /// </summary>
            /// <param name="userStatsTableEntity"></param>
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

            /// <summary>
            /// Retrieves ModeStats representing all game modes combined.
            /// </summary>
            /// <returns>ModeStats object representing all game modes.</returns>
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

            /// <summary>
            /// Finds the sum of the provided statistic name across all game modes.
            /// </summary>
            /// <param name="propertyName">Property name for which to find the sum.</param>
            /// <returns>Sum of given property across all game modes.</returns>
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

        /// <summary>
        /// Class containing essential statistics for a user pertaining to a single game mode.
        /// </summary>
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

            /// <summary>
            /// Constructs a ModeStats object. This constructor should be used for non-aggregate purposes.
            /// </summary>
            /// <param name="mode">Game mode.</param>
            /// <param name="stats">JObject representing a specific game mode's stats.</param>
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

            /// <summary>
            /// Constructs a ModeStats object. Use this constructor for aggregate purposes.
            /// Properties need to be assigned individually when creating an aggregate object
            /// as there is no corresponding JSON object stored in the Azure database.
            /// </summary>
            /// <param name="mode">Game mode or "aggregate".</param>
            public ModeStats(string mode)
            {
                if (mode != "aggregate")
                {
                    throw new InvalidOperationException();
                }

                this.Mode = mode;
            }
        }
    }
}
