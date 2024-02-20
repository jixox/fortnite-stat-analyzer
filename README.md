# Fortnite Stat Analyzer
Fortnite Stat Analyzer is a program that does exactly as its name suggests. It compiles user statistics provided by Epic and sends text messages to each subscriber outlining all players' stats in their cohort. This can be used to develop "power rankings" with an unhealthy competitive undertone within friend groups.
## Player Stats
The stats calculated for each player are as follows:
- Players bagged
- Matches played
- Minutes played
- K/M
- K/D
- Top 1, 3, 5, 10, and 25 placements
## Data Collection
Player Fortnite data is retrieved by supplying an Epic account ID to an API provided by _Fortnite API IO_. The response from this API call is a JSON object containing "global stats", i.e. cumulative all-time stats per game mode (solo, duo, trio, or squad).
## Stat Calculation
Stats are calculated by finding the delta of global stats over the course of a one day period. For example, _matches_played = global_stats<sub>2</sub>["solo"]["matchesplayed"] - global_stats<sub>1</sub>["solo"]["matchesplayed"]_
