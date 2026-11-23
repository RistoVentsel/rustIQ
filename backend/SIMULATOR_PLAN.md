# Polling strategy simulator: build plan

Brief for building the simulator that answers the thesis' first research question. Read
`STUDY_DATA_COLLECTION.md` first: it describes the measurement this simulator replays.

## 1. Goal

UK1: *Which polling strategy (the current four-lane one, uniform, or priority-based) gives the
freshest data at the same query volume when tracking about 10,000 active Rust servers, and at what
resource cost?*

The simulator must:

1. Compare three strategies on the same recorded data at the same query budget (A2S queries per
   minute), and repeat the comparison at several budgets.
2. Prove it is trustworthy. The simulated current strategy must reproduce what production actually
   measured over the same period. If it does not, no other result counts.
3. Produce the winning strategy as code that production can run later. That later step is not part
   of this brief, but the strategies must be written so it is possible.

It runs locally on exported CSV files, not on the VPS and not against any database.

## 2. Hard constraints

- Work on branch `school`. Never merge it into `main`. Do not push or deploy unless Risto asks.
- Production behaviour must not change before the measurement ends on 2026-11-18. Do not edit
  `ServerRepository`, the polling workers, `docker-compose*.yml` or any production options.
  New code goes into new files; the existing lane code stays as it is.
- Data and results live under `thesis/`, which is never committed. Nothing large goes into git.
- All existing tests (262) must keep passing. Add tests for the new code.
- .NET 10, xUnit, same style as the rest of the backend (see `backend/src/RustIQ.Application`).
- Deterministic: any randomness takes a seed from the command line, and the same input plus seed
  gives the same output.

## 3. Data

### Exports

`backend/scripts/export-simulator-data.sh` runs on the VPS and writes one gzip CSV per table.

| Export | Folder | Period |
| --- | --- | --- |
| 1 | `thesis/simulaator_andmed/20261115T2136Z` | 2026-11-04 00:00 to 2026-11-15 21:36 UTC |
| 2 (planned 2026-11-18) | `thesis/simulaator_andmed/<timestamp>` | 2026-10-27 00:00 to 2026-11-18 00:00 UTC |

Export 2 starts on 27.10 so the 7-day ranking window (section 4.4) has history from the start of the
period. Each folder has `manifest.txt` with row counts and sha256 sums; check them when loading.

**Merging is required.** Production retention deletes a pruned server together with all its
`server_snapshots`. Export 2 therefore lacks rows that export 1 has. The loader takes several export
folders and unions them:

- `server_snapshots`: by `id`;
- `servers`: by `id`, later export wins;
- `study_probes`: by `(server_idx, t_ms)`.

### Files and columns

Times are Unix milliseconds UTC (`t_ms`) in the two big files and ISO timestamps in UTC elsewhere.

| File | Columns | Notes |
| --- | --- | --- |
| `study_sample_servers` | server_idx, server_id, stratum, peak_players, country, selected_at | 450 servers, 150 per stratum (Popular / Medium / Small). `server_idx` 1 to 450 is stable across exports |
| `study_probes` | server_idx, t_ms, ok, players, queue | Ground truth. Every sample server every 15 s, failures included (`ok`=0, players empty). 30.8M rows in export 1 |
| `server_snapshots` | id, server_id, t_ms, players, max_players, online, ok, source | Every production poll. `source`: FastLane, NormalOnlineLane, SlowOfflineLane, VeryStaleOfflineLane, Watched, Discovery. `ok` is null for rows before 2026-11-03. 4.3M rows in export 1. **No queue column** |
| `servers` | id, server_type, status, players, max_players, queue, created_at, last_seen_at, last_checked_at, last_discovered_at, last_wipe_at, next_wipe_at | State at export time. Pruned servers are missing |
| `server_rankings` | server_id, rank, peak_players, generated_at | Ranking at export time |
| `study_sample_rankings` | Id, SampledAtUtc, ServerId, Rank | Hourly production rank of each sample server (empty rank = not ranked) |
| `study_population_samples` | Id, SampledAtUtc, TotalServers, OnlineServers, OnlineServersWithPlayers | Per minute. `TotalServers` = rows in `servers`, pruned ones included while they existed |
| `study_table_write_samples` | Id, SampledAtUtc, TableName, Inserts, Updates, Deletes | Per minute, cumulative counters per table (no resets in export 1) |
| `study_server_wipe_observations` | id, server_id, observed_at, last_wipe_at, previous_last_wipe_at, previous_next_wipe_at, first_since_start, keywords | Every `born` change of every server |
| `study_prediction_snapshots` | Id, ServerId, CapturedAtUtc, NextWipeAtUtc, DisplayedNextWipeAtUtc, LastWipeAtUtc | Hourly from 2026-11-13, sample servers only |

### Known facts about the data (verified on export 1)

- `study_probes`: 1800 rows per minute. Matches the write counters (30,842,505 inserts in the period),
  so nothing is missing.
- `server_snapshots`: 251 rows per minute exported, but the write counters show 4,818,383 inserts in
  the same period. About 514k rows (≈30 per minute) were deleted with pruned servers.

  | Lane | Exported, per minute | Measured on 12.11 |
  | --- | --- | --- |
  | Fast | 104.5 | 105 |
  | NormalOnline | 122.4 | 125 |
  | SlowOffline | 11.2 | 14 |
  | VeryStaleOffline | 11.1 | 30 |
  | Watched | 1.5 | |
  | Discovery | 0.4 | |

  The loss is almost entirely in the two offline lanes.
- **The production ranking was frozen.** `server_rankings.generated_at` is 2026-11-09 13:15 for all
  250 rows. `study_sample_rankings` shows sample ranks changing in only 12 of 290 hours, the last
  time at 2026-11-09 13h. The ranking worker (hourly) has mostly not been replacing the ranking. The
  cause is not established yet. The simulator must replay this behaviour, not an hourly ranking
  (section 4.4).

## 4. How the current strategy works (to port exactly)

Production code, read it before porting:

- `backend/src/RustIQ.Api/Workers/BattleMetricsPlayerCountRefreshWorker.cs`: the tick loop and lane order.
- `backend/src/RustIQ.Infrastructure/Servers/ServerRepository.cs`, `GetServersForRefreshLaneAsync`:
  which servers each lane picks.
- `backend/src/RustIQ.Application/Servers/ServerPollingService.cs`, `ApplyLatestServerState`: what a
  poll changes on the server row.
- `ServerRepository.ReplaceLatestServerRankingsAsync`: how the ranking is built.
- Values: `docker-compose.vps.yml`, `rustiq-api` service.

### 4.1 Tick loop

- The worker ticks every 15 s.
- Each tick, the lanes run in this order: Fast, NormalOnline, SlowOffline, VeryStaleOffline.
- A lane runs only when `now - lastRunOfThatLane >= interval`.
- When a lane runs, it picks up to `batch` servers with `(LastCheckedAtUtc ?? CreatedAtUtc) <= now - interval`.
- Servers already picked by an earlier lane in the same tick are skipped. They still count against
  the later lane's batch, because the query already took them.
- Order inside every lane: `LastCheckedAtUtc ?? CreatedAtUtc` ascending, then `Players` descending.
- Every server needs `Host != ''` and `QueryPort > 0`. All exported servers qualify.

| Lane | Interval | Batch |
| --- | --- | --- |
| Fast | 45 s | 100 |
| NormalOnline | 3 min | 400 |
| SlowOffline | 20 min | 500 |
| VeryStaleOffline | 15 min | 500 |

Other parameters: `PopularRankThreshold` 250, `PopularPlayerThreshold` 100,
`PopularPeakPlayerThreshold` 100, `RecentPeakWindow` 60 min, `PopularOfflineGrace` 60 min,
`SlowOfflineAfter` 60 min, `VeryStaleOfflineAfter` 24 h.

### 4.2 Lane membership

A server is *popular* if any of these holds:

- `Players >= 100`;
- `Status == Online && QueuePlayers > 0`;
- its rank `<= 250`;
- any snapshot in the last 60 min has `Players >= 100`;
- it is not online, `LastSeenAtUtc` is within the last 60 min, and it was ranked or had a 100+
  snapshot within those 60 min.

Lane membership:

| Lane | Members |
| --- | --- |
| Fast | Popular servers first. Leftover capacity goes to `ServerType == Official` servers |
| NormalOnline | Online, `Players < 100`, no queue, not ranked, no 100+ snapshot in the last 60 min |
| SlowOffline | Not online, `LastSeenAtUtc` between 24 h and 60 min ago, not ranked, no 100+ snapshot in the last 60 min |
| VeryStaleOffline | Not online, `LastSeenAtUtc` null or more than 24 h ago, not ranked, no 100+ snapshot in the last 60 min |

"Any snapshot" includes Watched and Discovery snapshots.

### 4.3 What a poll changes

The rules below come from `ApplyLatestServerState`.

| Poll result | Changes |
| --- | --- |
| Any | `LastCheckedAtUtc = t` |
| Answered | `Players`, `MaxPlayers` and `QueuePlayers` from the answer; `Status = Online`; `LastSeenAtUtc = t` |
| No answer and (`LastSeenAtUtc` is null or `t - LastSeenAtUtc > 600 s`) | `Status = Offline`, `Players = 0` |
| No answer, otherwise | Nothing else changes: the server stays online with its last counts |

A query is one or two attempts of up to 3 s each. The simulator may treat a poll as instantaneous at
the tick time.

### 4.4 Ranking

Rule: among servers seen in the last 24 h with a name and `MaxPlayers >= 1`, take those with at
least 6 snapshots in the last 7 days. Order them by average snapshot `Players` descending, then peak
descending, then id. The top 250 are ranked.

Because production only regenerated the ranking 12 times (section 3), the simulator regenerates it at
the same moments:

1. Take the hours in which any sample rank in `study_sample_rankings` changed.
2. Recompute the ranking from the simulated history at those moments.
3. Check the recomputed ranks of the 450 sample servers against `study_sample_rankings`. Report the
   agreement; it is part of validation.

Name and dedup by host:port are not in the export. Ignore them and note it.

## 5. Simulator design

### 5.1 Project layout

- `backend/src/RustIQ.Simulator`: a console app. It references `RustIQ.Domain` and
  `RustIQ.Application`, has no EF and no database, and is added to `RustIQ.sln`.
- Strategy interface and strategies go in `backend/src/RustIQ.Application/Servers/Polling/`. They are
  pure in-memory code with no I/O, so a production worker can use them later.
- Tests go in `backend/tests/RustIQ.Tests/Simulation/`.

### 5.2 Engine

- A virtual clock steps 15 s at a time from `--from` to `--to`. Default period: 2026-11-04 00:00 to
  2026-11-18 00:00 UTC.
- Each step, the strategy is asked which servers to poll. The engine answers each poll from the
  response oracle (5.3), updates the server state with the rules in 4.3, and logs the poll.
- Per-server state holds exactly what the strategies may use:
  - `LastCheckedAtUtc`, `LastSeenAtUtc`, `Status`, `Players`, `MaxPlayers`, `QueuePlayers`;
  - `CreatedAtUtc`, `ServerType`, rank;
  - recent snapshot players (for the 60 min peak rule);
  - for the priority strategy, its own estimates.
- **Fairness rule:** a strategy only knows what its own polls (plus the exogenous ones in 5.4)
  returned. It never reads ground truth or production data directly.
- Memory: keep probes in per-server arrays sorted by time (time, players, ok), not objects. 30.8M
  probes should fit in a few hundred MB.

### 5.3 Response oracle

| Server kind | Answer at simulated time t |
| --- | --- |
| Sample server | The latest probe in `(t-20 s, t]`. None means no answer; otherwise its `ok` and players |
| Other known server | The production snapshot nearest to t (either side): its `ok` and players. Before 2026-11-03, `ok` is null; use `online` |
| Phantom server (5.5) | Never answers |

Non-sample servers are only known at production's poll times, which is coarse. This only affects
how budget and lane membership are spent, not the measured metrics. The thesis lists it as a
limitation.

### 5.4 Exogenous polls

Production `Watched` polls (a user on a detail page) and `Discovery` polls happen regardless of the
strategy. Replay them from `server_snapshots` in every strategy as state updates:

- They update `LastCheckedAtUtc` and the other state.
- They do not count against the budget.
- For AoI, Discovery polls count and Watched polls do not, as in the baseline query.

### 5.5 Phantom servers

The loss described in section 3 means part of the population is missing. Fill the gap so that the
simulated server count per minute matches `study_population_samples.TotalServers`:

1. A known server exists from `created_at` (or its first snapshot, whichever is earlier) until it is
   still in the latest `servers` file. If it is no longer there, it exists until its last snapshot.
2. `phantoms(t) = max(0, TotalServers(t) - known(t))`.
3. Of them, `max(0, OnlineServers(t) - known servers the oracle answers for at t)` answer (counted
   hourly), with no players and 100 max players, so the online/offline split follows
   `study_population_samples.OnlineServers`. The export lost the whole history of servers that were
   online and later pruned. The rest never answer and have `LastSeenAtUtc` null, so the current
   strategy puts them in VeryStaleOffline.
4. When the answering count falls while the silent count rises, the oldest answering phantom stops
   answering (it went offline), and the other way round. Otherwise phantoms join and leave in FIFO
   order, silent ones first. Answering phantoms present at the start begin online, last polled at a
   random moment of the previous 75 min; later ones join never polled.

Validation should show VeryStaleOffline at roughly 30 polls per minute, not 11.

## 6. Strategies

All strategies implement the same interface and get the same per-step budget. The current strategy
has no budget parameter; its budget is whatever its lanes produce. For the budget sweep, scale its
batch sizes by a factor.

### 6.1 FourLane

The port of section 4, line for line. Parameters come from options with the production defaults.

Option `--ranking frozen|hourly`, default `frozen`:

- `frozen` replays production as it ran: regenerations only at the 12 detected moments (4.4). This
  is used for validation and for the main comparison.
- `hourly` regenerates every hour, as the code intends. This is used once, as a check that the
  ranking freeze does not change the conclusions.

Uniform and Priority do not use the ranking.

### 6.2 Uniform

Each step, poll the `budget/4` servers with the oldest `LastCheckedAtUtc ?? CreatedAtUtc`, phantoms
included. Ties are broken by id. There is no special treatment.

### 6.3 Priority

Each step, poll the top `budget/4` eligible servers by score:

```
score = w * (1 - exp(-lambda * age))
```

| Term | Meaning |
| --- | --- |
| `age` | `t - LastCheckedAtUtc` |
| `w` | `1 + EWMA` of the server's observed players. Stale data on busy servers costs more |
| `lambda` | Estimated rate at which the player count changes, from the strategy's own polls: changes seen divided by time observed, smoothed with a prior. Cho and Garcia-Molina show that pure proportional-to-change refreshing is worse than uniform, so the score must stay bounded in `lambda` (the exponential does this) |

Eligibility and modifiers:

- **Minimum interval:** a server is not polled again within `--min-interval` (default 45 s).
- **Offline back-off:** after k consecutive unanswered polls, the server's minimum interval becomes
  `min-interval * 2^k`, capped at `--max-backoff` (default 6 h).
- **Wipe boost:** within ±1 h of RustIQ's own predicted next wipe (`next_wipe_at`, or the forced
  wipe on the first Thursday at 19:00 London time), multiply the score by `--wipe-boost` (default 3).
  This uses only information production has.

Tune the parameters on 2026-11-04 to 2026-11-11 only. Report results for the full period and
separately for 2026-11-11 to 2026-11-18, so the reported numbers are not fitted to the data they
are judged on.

All defaults above are proposals. Keep them configurable from the command line.

## 7. Evaluation (identical for real and simulated polls)

These are the rules of Appendix 2, query 1 in the thesis, which produced Table 5:

1. Observation grid: every 15 min from the period start, for every sample server.
2. A point counts only if that server has a probe with `ok = 1` in `(t-20 s, t]`. The truth is that
   probe's players.
3. "Shown" is the latest poll with `ok = 1` at or before t, excluding Watched; Discovery counts. In
   a simulation that means the simulated polls plus replayed Discovery polls.
4. `AoI = t - time of that poll`. A point with no such poll counts in `points` but not in the averages.
5. Per stratum report:
   - points and points with a value;
   - AoI mean, median and p95 (`percentile_cont`, linear interpolation);
   - player MAE;
   - share of points with |error| > 5.
6. Also report:
   - player-weighted AoI per stratum and overall: `sum(true_players * AoI) / sum(true_players)`;
   - polls per minute per lane or strategy;
   - share of polls not answered.

**Check the evaluator first.** Run it on the real production snapshots for 2026-11-05 00:00 to
2026-11-12. It must closely reproduce Table 5. The exact end time of that query run is unknown, so
small differences are expected.

| Stratum | Median AoI | Mean AoI | p95 AoI | MAE | > 5 off |
| --- | --- | --- | --- | --- | --- |
| Popular | 136 s | 1036 s | 4120 s | 3.07 | 13.1 % |
| Medium | 1973 s | 2101 s | 4523 s | 2.64 | 10.4 % |
| Small | 2395 s | 2552 s | 4568 s | 0.42 | 0.4 % |

## 8. Validation of the simulator

Run FourLane over the full period and compare it with the evaluator on the real snapshots for the
same period:

1. AoI mean and median per stratum.
2. Polls per minute per lane. Use the measured values and the write counters as the reference;
   remember that the exported snapshots lack about 30 per minute of offline-lane polls.
3. Polls per sample server per lane, simulated against real. This shows where the simulation
   diverges.
4. Ranking agreement on the sample servers (4.4).

Acceptance thresholds:

- AoI mean and median per stratum within 20 % of real;
- polls per minute per lane within 10 % of real. Fast and NormalOnline against the exported rate of
  the period; the offline lanes together against the 44 per minute measured on 2026-11-12 around noon
  UTC over the previous 24 h, with the simulation taken over 2026-11-11 12:00 to 11-12 12:00 UTC.
  The full period's offline lanes are also shown against the write
  counter, an upper bound.

Player-weighted AoI has no threshold; it is reported for the whole period and per half (before and
after the tuning split).

Write the outcome down even if it fails, and find the cause before comparing strategies.

## 9. Runs and output

Example command:

```
dotnet run --project backend/src/RustIQ.Simulator -- --data <export dir> [<export dir> ...]
    --from 2026-11-04T00:00Z --to 2026-11-18T00:00Z --strategy fourlane|uniform|priority
    --budget <queries/min> --seed <n> --out thesis/simulaator_tulemused/<run name>
```

| Run | What |
| --- | --- |
| Validation | `fourlane` at production settings |
| Main comparison | `uniform` and `priority` at the budget fourlane actually used in the simulation |
| Budget sweep | All three at 150, 200, 275, 350 and 450 queries per minute (proposed). Fourlane scales its batch sizes |
| Ranking check | `fourlane --ranking hourly` at production settings, compared with the frozen run |

Each run writes a folder with:

- `summary.csv`: one row per stratum with every metric in section 7;
- `polls_per_minute.csv`;
- `run.json`: arguments, seed, data manifests and git commit;
- for the validation run, the per-sample-server comparison.

Small plain files are best, because the numbers go into thesis tables.

## 10. Order of work

1. Loader with export merging and manifest checks. Test it on export 1.
2. Evaluator. Check it against Table 5 on the real snapshots.
3. Engine, response oracle, exogenous polls and phantoms.
4. FourLane port and ranking replay. Validation run on export 1.
5. Uniform and Priority, budget sweep.
6. On 2026-11-18, once export 2 exists: all runs on the full period.

Steps 1 to 5 run on export 1 now. The final numbers come from step 6.

## 11. Open decisions (Risto)

- Validation thresholds (section 8).
- Priority defaults: minimum interval 45 s, back-off cap 6 h, wipe boost 3 (section 6.3).
- Budget sweep values (section 9).
