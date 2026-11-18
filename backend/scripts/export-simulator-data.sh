#!/usr/bin/env bash
# Exports the data the polling-strategy simulator needs from the rustIQ database on the VPS.
#
# Usage:  bash export-simulator-data.sh [FROM] [TO]
#   FROM  defaults to 2026-11-04 00:00 UTC (start of the compared period)
#   TO    defaults to the current minute (UTC)
#
# Output: ~/rustiq-export/<UTC timestamp>/ with one gzip CSV per table and manifest.txt.
# Times in the big tables are Unix milliseconds (UTC); the small tables keep ISO timestamps in UTC.
#
# Snapshot retention deletes a pruned server together with its server_snapshots, so the export is run
# more than once and the snapshot files are merged by id. The study_* tables are never pruned.
set -euo pipefail

FROM="${1:-2026-11-04 00:00+00}"
TO="${2:-$(date -u '+%Y-%m-%d %H:%M+00')}"
CONTAINER="rustiq-postgres"
OUT="$HOME/rustiq-export/$(date -u '+%Y%m%dT%H%MZ')"

free_kb=$(df --output=avail -k "$HOME" | tail -1)
if [ "$free_kb" -lt $((2 * 1024 * 1024)) ]; then
    echo "Less than 2 GB free in $HOME, stopping." >&2
    exit 1
fi

mkdir -p "$OUT"
echo "Exporting $FROM .. $TO into $OUT"

# Reads a SELECT from stdin and writes its result as a gzip CSV file.
export_csv() {
    local name=$1
    local started=$SECONDS
    {
        echo "SET TimeZone = 'UTC';"
        echo "COPY ("
        cat
        echo ") TO STDOUT WITH (FORMAT csv, HEADER);"
    } | docker exec -i "$CONTAINER" psql -U rustiq -d rustiq -X -q -v ON_ERROR_STOP=1 \
      | gzip -6 > "$OUT/$name.csv.gz"
    echo "  $name ($((SECONDS - started)) s)"
}

# The sample is fixed, so its order gives every export the same server_idx.
export_csv study_sample_servers <<SQL
SELECT row_number() OVER (ORDER BY "Stratum", "ServerId") AS server_idx,
       "ServerId" AS server_id,
       "Stratum" AS stratum,
       "PeakPlayersAtSelection" AS peak_players,
       "Country" AS country,
       "SelectedAtUtc" AS selected_at
FROM study_sample_servers
SQL

export_csv study_probes <<SQL
WITH s AS (
    SELECT "ServerId", row_number() OVER (ORDER BY "Stratum", "ServerId") AS server_idx
    FROM study_sample_servers
)
SELECT s.server_idx,
       (extract(epoch FROM p."ProbedAtUtc") * 1000)::bigint AS t_ms,
       p."Succeeded"::int AS ok,
       p."Players" AS players,
       p."QueuePlayers" AS queue
FROM study_probes p
JOIN s USING ("ServerId")
WHERE p."ProbedAtUtc" >= '$FROM' AND p."ProbedAtUtc" < '$TO'
SQL

export_csv server_snapshots <<SQL
SELECT "Id" AS id,
       "ServerId" AS server_id,
       (extract(epoch FROM "CheckedAtUtc") * 1000)::bigint AS t_ms,
       "Players" AS players,
       "MaxPlayers" AS max_players,
       "IsOnline"::int AS online,
       "QuerySucceeded"::int AS ok,
       "PollSource" AS source
FROM server_snapshots
WHERE "CheckedAtUtc" >= '$FROM' AND "CheckedAtUtc" < '$TO'
SQL

export_csv servers <<SQL
SELECT "Id" AS id,
       "ServerType" AS server_type,
       "Status" AS status,
       "Players" AS players,
       "MaxPlayers" AS max_players,
       "QueuePlayers" AS queue,
       "CreatedAtUtc" AS created_at,
       "LastSeenAtUtc" AS last_seen_at,
       "LastCheckedAtUtc" AS last_checked_at,
       "LastDiscoveredAtUtc" AS last_discovered_at,
       "LastWipeAtUtc" AS last_wipe_at,
       "NextWipeAtUtc" AS next_wipe_at
FROM servers
SQL

export_csv server_rankings <<SQL
SELECT "ServerId" AS server_id, "Rank" AS rank, "PeakPlayers" AS peak_players, "GeneratedAtUtc" AS generated_at
FROM server_rankings
SQL

# The small study tables are exported whole up to TO; wipe history before FROM is needed too.
export_csv study_server_wipe_observations <<SQL
SELECT "Id" AS id,
       "ServerId" AS server_id,
       "ObservedAtUtc" AS observed_at,
       "LastWipeAtUtc" AS last_wipe_at,
       "PreviousLastWipeAtUtc" AS previous_last_wipe_at,
       "PreviousNextWipeAtUtc" AS previous_next_wipe_at,
       "IsFirstObservationSinceStart"::int AS first_since_start,
       "Keywords" AS keywords
FROM study_server_wipe_observations
WHERE "ObservedAtUtc" < '$TO'
SQL

export_csv study_prediction_snapshots <<SQL
SELECT * FROM study_prediction_snapshots WHERE "CapturedAtUtc" < '$TO'
SQL

export_csv study_sample_rankings <<SQL
SELECT * FROM study_sample_rankings WHERE "SampledAtUtc" < '$TO'
SQL

export_csv study_population_samples <<SQL
SELECT * FROM study_population_samples WHERE "SampledAtUtc" < '$TO'
SQL

export_csv study_table_write_samples <<SQL
SELECT * FROM study_table_write_samples WHERE "SampledAtUtc" < '$TO'
SQL

{
    echo "from: $FROM"
    echo "to:   $TO"
    echo "exported_at: $(date -u '+%Y-%m-%d %H:%M:%S+00')"
    echo
    echo "rows (without header):"
    for f in "$OUT"/*.csv.gz; do
        printf '  %-40s %s\n' "$(basename "$f")" "$(( $(zcat "$f" | wc -l) - 1 ))"
    done
    echo
    echo "sha256:"
    (cd "$OUT" && sha256sum ./*.csv.gz)
} > "$OUT/manifest.txt"

cat "$OUT/manifest.txt"
du -sh "$OUT"
echo "Done: $OUT"
