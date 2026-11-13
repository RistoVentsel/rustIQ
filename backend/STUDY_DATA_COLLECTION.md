# Thesis data collection

What the `Study` feature collects for the thesis (UK1: polling strategies, UK2: wipe prediction),
and how to get the data out. Nothing here changes what RustIQ shows or how it polls.

## Two processes

`docker-compose.vps.yml` runs the measurement in its own container, so its load stays out of the
baseline resource cost:

- `rustiq-api` (role `api`): production as before. It records its own resource use, the database
  wide figures (table writes, server population, sample ranks) and wipe observations for every server.
- `rustiq-study` (role `study`): the same image with every production worker switched off. It runs
  only the dense probes and the BattleMetrics captures, and records its own resource use.

Both run the migrations on start; EF Core serialises them with a lock.

To update only the study container, pass `--no-deps`. Without it Compose also rebuilds and restarts
`rustiq-api`, because `study` depends on it, which moves the baseline to a new commit:

```bash
docker compose -f docker-compose.vps.yml up -d --build --no-deps study
```

On 2026-11-03 around 17:20 UTC this happened once: the API moved from 48a27ce to 6b6606c. The polling
code is identical between the two; the restart left a gap of a few seconds in the snapshots and a
second set of first-sighting rows in `study_server_wipe_observations`.

## What is collected

| Table | What | How often |
| --- | --- | --- |
| `study_sample_servers` | The frozen stratified sample: popular / medium / small by the 24 h peak player count, at most 25 % from one country per stratum, fixed seed | Once, on the first start |
| `study_probes` | One A2S_INFO probe per sample server, failures included | Every 15 s per server |
| `study_wipe_signals` | Sample servers: `born` (last wipe), the next wipe RustIQ derives from the server's own tags, and the keywords minus player counts | Only when one of them changes |
| `study_battlemetrics_wipe_snapshots` | BattleMetrics' next wipe for each sample server. Off since 2026-11-03: every request got 403 (see below) | Every 12 h when on |
| `study_server_descriptions` | Each sample server's A2S rules description, where many servers state their wipe schedule | Every 12 h |
| `study_prediction_snapshots` | Sample servers: RustIQ's stored next wipe, the next wipe the pages show (none once it has passed) and the last wipe, so a BattleMetrics value read by hand can be compared with RustIQ's prediction from the same hour. Added 2026-11-13 | Every 60 min |
| `study_server_wipe_observations` | Every server: a change in `born`, with what RustIQ had as the last and next wipe right before it | When `born` changes, plus once per server after each restart |
| `study_resource_samples` | Per process (`Role`): cumulative CPU ms, memory, network bytes, A2S packets sent, BattleMetrics requests | Every 60 s |
| `study_population_samples` | Servers tracked, online, online with players | Every 60 s |
| `study_table_write_samples` | `pg_stat_user_tables` inserts / updates / deletes per table | Every 60 s |
| `study_sample_rankings` | Each sample server's rank (the fast lane admits by rank; `server_rankings` keeps only the latest) | Every 60 min |
| `server_snapshots.QuerySucceeded`, `.PollSource` | For the production strategy: whether the A2S query was answered, and which lane polled | Every production poll |

`IsOnline` in `server_snapshots` is not usable for freshness: a failed query inside the 10 minute
offline grace still records the server as online. Use `QuerySucceeded`. Rows from before this change
have it as NULL, so the baseline is measured from the deploy onwards, not from older history.

Snapshot retention is raised to 60 days. The study tables have no retention and no foreign key to
`servers`, so pruning a server does not delete its measurements.

## Before the study starts

1. Deploy, then check both containers:

   ```bash
   docker logs rustiq-study 2>&1 | grep Study
   docker logs rustiq-api 2>&1 | grep Study
   ```

   Expected: `Study sample selected`, `Study probe worker started` and `Study measurement worker
   started` in the study log; `Study measurement worker started. Role: api` in the API log.
2. Write down the sample definition for the thesis:

   ```sql
   SELECT "Stratum", count(*), min("PeakPlayersAtSelection"), max("PeakPlayersAtSelection"),
          count(DISTINCT "Country") AS countries, min("SelectedAtUtc")
   FROM study_sample_servers GROUP BY "Stratum";
   ```
3. Record the commit hash and the lane settings in `docker-compose.vps.yml`. They are the baseline
   strategy and must not change while the study runs.
4. Check that the VPS clock is synchronised (`timedatectl` should say `System clock synchronized: yes`).
5. BattleMetrics: on 2026-11-03 its public pages (`/servers/rust`, `/servers/rust/<id>/rust-settings`)
   returned 403 (a Cloudflare challenge) from a home connection and from the VPS alike, and the first
   capture round got `WithNextWipe: 0` for all 450 servers. RustIQ does not work around that, so the
   automatic captures are switched off (`Study__BattleMetricsSnapshotsEnabled: "false"`). Production's
   own BattleMetrics fallback fails the same way and is left as it is, because it is part of the
   baseline. Reference values for a 45-server subsample are taken by hand instead (`bm-manual.csv`).
   To check whether the block has lifted:

   ```bash
   docker exec rustiq-study curl -s -o /dev/null -w '%{http_code}\n' --compressed \
     -A 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/138.0.0.0 Safari/537.36' \
     'https://www.battlemetrics.com/servers/rust'
   ```

   200 is fine. A NULL `NextWipeAtUtc` means either "BattleMetrics lists no upcoming wipe" or "the
   request failed", and the table cannot tell them apart. Watch the log line
   `Study BattleMetrics wipe capture completed ... WithNextWipe: N`: 0 for the whole sample almost
   certainly means the requests are blocked.

## Checks while it runs

```sql
-- Probes in the last minute; about 450 servers * 4 = 1800 is expected
SELECT count(*), avg("Succeeded"::int) FROM study_probes WHERE "ProbedAtUtc" > now() - interval '1 minute';

-- Last BattleMetrics capture
SELECT max("CapturedAtUtc"), count(*) FILTER (WHERE "NextWipeAtUtc" IS NOT NULL) FROM study_battlemetrics_wipe_snapshots
WHERE "CapturedAtUtc" > now() - interval '13 hours';

-- Both processes are sampling
SELECT "Role", max("SampledAtUtc") FROM study_resource_samples GROUP BY 1;

-- Disk use
SELECT relname, pg_size_pretty(pg_total_relation_size(relid)) FROM pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC;
```

## Turning the counters into rates

The resource and table counters are cumulative. Rates are the difference between consecutive rows
of the same role or table; a negative difference marks a restart and that interval is dropped.

```sql
-- CPU % of one core, network kB/s and A2S packets per minute, per process and minute
SELECT "Role", "SampledAtUtc",
       100.0 * ("ProcessCpuMilliseconds" - lag("ProcessCpuMilliseconds") OVER w)
             / (1000 * extract(epoch FROM "SampledAtUtc" - lag("SampledAtUtc") OVER w)) AS cpu_percent,
       ("NetworkSentBytes" + "NetworkReceivedBytes"
         - lag("NetworkSentBytes" + "NetworkReceivedBytes") OVER w) / 1024.0
             / extract(epoch FROM "SampledAtUtc" - lag("SampledAtUtc") OVER w) AS network_kb_per_s,
       ("A2sPacketsSent" - lag("A2sPacketsSent") OVER w) * 60.0
             / extract(epoch FROM "SampledAtUtc" - lag("SampledAtUtc") OVER w) AS a2s_packets_per_min
FROM study_resource_samples
WINDOW w AS (PARTITION BY "Role", "ProcessStartedAtUtc" ORDER BY "SampledAtUtc")
ORDER BY 1, 2;

-- Database row writes per minute by production tables vs. study tables
SELECT "SampledAtUtc", "TableName" LIKE 'study_%' AS is_study,
       sum("Inserts" + "Updates" + "Deletes" - lag_writes) AS writes
FROM (SELECT *, lag("Inserts" + "Updates" + "Deletes") OVER (PARTITION BY "TableName" ORDER BY "SampledAtUtc") AS lag_writes
      FROM study_table_write_samples) t
WHERE lag_writes IS NOT NULL
GROUP BY 1, 2 ORDER BY 1, 2;
```

## Export

Run on the VPS. The files land in the current directory:

```bash
for t in study_sample_servers study_probes study_wipe_signals study_battlemetrics_wipe_snapshots \
         study_server_wipe_observations study_resource_samples study_population_samples \
         study_table_write_samples study_sample_rankings; do
  docker exec rustiq-postgres psql -U rustiq -d rustiq -c "\copy $t TO STDOUT WITH CSV HEADER" > "$t.csv"
done
docker exec rustiq-postgres psql -U rustiq -d rustiq -c "\copy (SELECT \"ServerId\", \"CheckedAtUtc\", \"Players\", \"IsOnline\", \"QuerySucceeded\", \"PollSource\" FROM server_snapshots WHERE \"QuerySucceeded\" IS NOT NULL) TO STDOUT WITH CSV HEADER" > server_snapshots.csv
```

## Notes for the analysis

- A wipe is a `study_server_wipe_observations` row with `IsFirstObservationSinceStart = false` and a
  later `LastWipeAtUtc` than `PreviousLastWipeAtUtc`. Its `PreviousNextWipeAtUtc` is what RustIQ was
  showing as the next wipe just before, i.e. the current system's prediction. Distinct
  `(ServerId, LastWipeAtUtc)` pairs collapse the repeated first rows after restarts.
- The rows before a wipe in `study_wipe_signals` are what a predictor could know at that time, so
  predictions can be rebuilt afterwards without leaking the outcome.
- `born` marks a map wipe. Blueprint wipes are not visible over A2S, and the BattleMetrics parser
  also reads map wipes only.
- Not every `born` change is a wipe in the players' sense. In the first hour, several servers
  reset to a new save every hour on the hour or every 1.5 h (arena / minigame style), and a few
  alternated between two `born` values, i.e. went back to an older save. Before analysing UK2,
  drop changes where `born` goes backwards and treat servers with very short cycles as their own
  group, and state the rule in the methodology.
- Force wipes (first Thursday of the month, around 19:00 UTC) are predictable from the calendar.
  Report them apart from the servers' own schedule wipes, or the predictor looks better than it is.
- The lanes share fixed batch sizes across all servers. To compare strategies at the same query
  volume on the sample, scale the budget by sample size over `study_population_samples.TotalServers`,
  and take the current strategy's real budget from `server_snapshots` per `PollSource`.
- Ground truth resolution is 15 s, so freshness below that cannot be measured. Polls with
  `PollSource = 'Watched'` (a detail page open) come every few seconds and are denser than the
  ground truth; leave them out of the baseline or report them separately.
- Probes use the same A2S client as production (2 attempts, 3 s timeout each), so a failed probe
  means no answer in about 6 s. `A2sPacketsSent` counts every packet, retries included.
- `BattleMetricsRequests` counts paced requests; curl's own retries on a failed request are not
  counted separately.
