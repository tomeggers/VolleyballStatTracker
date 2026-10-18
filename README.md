# Volleyball Stat Tracker

A live stat tracker for coaching a club volleyball team. Tap a player, tap what they did, and the score and stats update as the match goes.

Built with C#, ASP.NET Core Razor Pages, Entity Framework Core and SQL Server.

## What it does

- **Players:** add your squad with names and shirt numbers.
- **Matches:** create a match against an opponent and go straight into logging.
- **Live logging:** tap a player, then an action. Point-scoring actions update the score on their own. Kills, aces and blocks are green (our point), errors are red (their point), and everything else is grey (rally continues).
- **Undo:** removes the last entry and fixes the score, because mis-taps happen mid-rally.
- **Opponent point:** records a point the other team won outright, with no player of ours attached.
- **Pass grading:** serve receive is graded 0 to 3, the standard coaching scale. A 0 gives the opponent the point.

Stats tracked: kill, attack error, attack in play, ace, serve error, serve in play, solo block, block assist, dig, assist, reception (0 to 3), opponent point.

## How it works

Every action is stored as its own row (`StatEntry`). The score, totals and percentages are all worked out from those rows instead of being stored as running counters. That means totals can't drift out of sync, undo is just deleting the last row, and new stats can be calculated for matches that were logged before the stat existed.

Which actions win or lose a rally lives in one place, `StatRules.GetPointResult`, so the scoreboard and the stats always agree.

```
Match 1──* Set 1──* StatEntry *──1 Player (optional)
                        │
                    StatRules ──> score, totals, hitting %
```

## Running it locally

Needs the .NET 10 SDK and SQL Server (Express is fine).

```bash
dotnet tool install --global dotnet-ef   # once
dotnet ef database update                # creates the database
dotnet run
```

The connection string in `appsettings.json` points to `.\SQLEXPRESS` with Windows authentication. Change it if your SQL Server is somewhere else.

## Roadmap

- [ ] Next set / end match, and a match list
- [ ] Instant taps (no page reload)
- [ ] Match summary: hitting %, pass average and serve stats per player
- [ ] Unit tests and CI
- [ ] Season totals and auto-suggested awards
- [ ] Deploy to Azure for courtside use on a phone
