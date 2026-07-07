# RustIQ Backend Step-by-Step Plan

This plan assumes the original one-server MVP is already done and verified locally.

What already works:

- PostgreSQL persistence works
- Cached REST endpoints exist
- SignalR live updates exist
- The frontend can open one server page and watch updates live

The backend direction has now changed based on a real product constraint:

> Raw A2S server summary data is not reliable enough to be the primary source for a large Rust server directory.

For RustIQ, the backend should now use this model:

- BattleMetrics as the upstream source for server discovery and summary population
- RustIQ PostgreSQL as the product source of truth
- A2S only where it remains useful
- RCON later where available, not as a requirement for the public directory

That means:

- server list population and cached summary data should come from BattleMetrics-backed ingestion
- player names can still come from A2S where possible
- RustIQ should not depend on BattleMetrics page rendering or frontend websocket behavior
- RustIQ should own its own storage, API, refresh cadence, and user-facing read model

The next backend goal is:

> Build a scalable Rust server summary backend that can ingest and store large numbers of Rust servers using BattleMetrics as the upstream directory source.

## Phase 0: Lock the New Product/Data Direction

The backend should now be explicit about source roles:

- `BattleMetrics`: discovery and server summary source
- `A2S`: optional current-player-list source and diagnostic source
- `RCON`: future premium/high-accuracy source when available
- `PostgreSQL`: RustIQ’s own source of truth for serving the product

Important product rules:

- The frontend must still read only from RustIQ APIs
- RustIQ should cache, store, and serve summary data from its own database
- RustIQ should not rely on BattleMetrics frontend pages
- RustIQ should not rely on undocumented BattleMetrics websocket internals
- RustIQ should not pretend all servers have equally trustworthy player-list data

Do not build:

- Authentication
- Admin dashboard
- Billing
- Content systems
- Manual one-by-one server overrides as the main scaling strategy

Done when:

- The backend roadmap is centered on scalable ingestion and cached serving, not direct live querying of every server page.

## Phase 1: Introduce Explicit Source Roles in the Model

Right now the code has server polling behavior, but the data-source model should be made explicit.

Add explicit source concepts such as:

- `ServerSummarySource`
- `PlayerListSource`
- optionally `DataQuality`
- optionally `LastValidationReason`

Recommended enum values:

- `BattleMetrics`
- `A2S`
- `Rcon`
- `Unknown`

Recommended first use:

- all ingested public directory servers default to `ServerSummarySource = BattleMetrics`
- `PlayerListSource = A2S` for now

Do not over-model yet:

- no per-field source tracking yet
- no giant scoring engine yet

Done when:

- The backend can say where a server’s summary data came from.

## Phase 2: Separate Summary Ingestion from Player-List Querying

The current backend logic mixes “server summary” and “player list” behavior too closely.

Refactor the backend into two clearly separate flows:

1. Summary ingestion flow
   - source: BattleMetrics
   - purpose: name, host, ports, status, players, max players, map summary, timestamps

2. Optional player-list enrichment flow
   - source: A2S
   - purpose: current player names when available

Rules:

- The list page and summary APIs should not depend on A2S
- If A2S player-list enrichment fails, server summary data should still remain valid
- Summary ingestion must scale independently from player-list enrichment

Done when:

- RustIQ can continue serving valid server summaries even if A2S player queries are weak or unavailable.

## Phase 3: Build BattleMetrics Discovery Ingestion

Move from a tiny manual server list toward scalable ingestion.

Implement a BattleMetrics-backed discovery/import worker or service that can:

1. Fetch Rust servers from BattleMetrics
2. Read server summary fields
3. Upsert them into RustIQ
4. Store enough identity information to refresh them later

Useful fields to ingest:

- BattleMetrics server id
- Name
- Address/hostname if present
- IP
- Game port
- Query port
- Players
- Max players
- Status
- Country if useful
- Map name if present
- Last BattleMetrics update timestamp if present

Important:

- RustIQ should store BattleMetrics server id as an upstream identifier
- Host + query port still matter, but BattleMetrics id should be treated as a stable upstream key when available

Done when:

- RustIQ can import more than a hand-written server list.

## Phase 4: Add BattleMetrics Identity Fields to Persistence

The current database model is still shaped around host/query-port only.

Add persistence support for upstream identity such as:

- `BattleMetricsServerId`
- optional `Address`
- optional `GamePort`
- optional `Country`
- optional `SummarySource`

Keep the existing core fields:

- Host
- QueryPort
- Name
- Players
- MaxPlayers
- Status
- timestamps

Migration goals:

- existing data should remain usable
- new ingested servers should be keyed and updated safely

Done when:

- RustIQ can persist and re-identify upstream BattleMetrics servers cleanly.

## Phase 5: Replace Tiny Manual Multi-Server Polling with Summary Refresh by Upstream Identity

Once BattleMetrics discovery exists, the backend should stop pretending the primary problem is polling a tiny config list forever.

Instead, summary refresh should be:

- based on stored RustIQ servers
- refreshed from BattleMetrics using upstream identity
- updated on a worker cadence appropriate for a directory product

Recommended behavior:

- recent/high-interest servers can refresh more often later
- initial version can use a simple batch refresh cadence

Do not do this yet:

- query every server directly via A2S for summary data
- force full-directory real-time updates for every client

Done when:

- RustIQ summary freshness is driven by ingestion/refresh jobs, not per-page direct queries.

## Phase 6: Keep A2S Only for What It Still Does Well Enough

A2S should now have a narrower role.

Use A2S for:

- current player list when available
- optional diagnostics/comparison
- maybe direct detail enrichment later

Do not use A2S as the primary truth for:

- large-scale directory player counts
- universal server summary population

Important behavior:

- if A2S player names fail, do not degrade summary correctness
- if A2S data looks suspicious, keep it out of the main summary model

Done when:

- A2S is an enrichment source, not the backbone of the public directory.

## Phase 7: Add BattleMetrics Batch Refresh Strategy

Once servers are ingested, build a refresh loop for summary data.

The backend should support:

- refreshing stored servers in batches
- protecting against huge refresh bursts
- pacing requests so RustIQ can scale up

Design goals:

- avoid naively refreshing every stored server every few seconds
- start with predictable batch windows
- later prioritize high-population or recently-viewed servers

Useful controls:

- batch size
- refresh interval
- stale threshold
- optional prioritization rules later

Done when:

- RustIQ can refresh many servers without turning the worker into a constant hammer.

## Phase 8: Add Data Quality and Validation Signals

Because upstream data sources vary in quality, record simple validation signals.

Examples:

- `PlayersExceedMaxPlayers`
- `SummarySourceUnavailable`
- `PlayerListUnavailable`
- `IdentityConflict`
- `A2SDetailMismatch`

Do not build a giant scoring engine yet.

First pass:

- store a small quality/status field or last validation reason
- use it for debugging and future filtering

Done when:

- RustIQ can flag suspicious server data instead of silently treating all rows as equal.

## Phase 9: Keep the Cached API Read Model Stable

The frontend contract should still stay simple:

```http
GET /servers
GET /servers/{id}
GET /servers/{id}/snapshots
GET /servers/{id}/players
GET /servers/{id}/map
```

The difference is where the data came from internally:

- `GET /servers` should now serve BattleMetrics-ingested summary data from PostgreSQL
- `GET /servers/{id}/players` can still serve A2S-derived player lists when available

Important:

- frontend consumers should not need to know whether data came from BattleMetrics or A2S unless you explicitly choose to expose source metadata

Done when:

- RustIQ APIs remain stable while internal source strategy changes.

## Phase 10: Define Snapshot Retention and Growth Rules

As the number of tracked servers grows, snapshot volume matters much more.

Keep:

- raw snapshots for a limited window
- scheduled cleanup

Do not build yet:

- hourly/daily rollups
- archival pipelines
- warehouse-style analytics

Done when:

- directory growth does not imply unlimited snapshot growth.

## Phase 11: Controlled Expansion Path to Large Coverage

After BattleMetrics ingestion works, scale in stages:

1. 10 servers
2. 100 servers
3. 500 servers
4. 1000+ servers

At each stage verify:

- ingestion duration
- DB write volume
- refresh loop health
- API read latency
- duplicate/upsert correctness
- snapshot growth

Do not jump from tiny test set to “every Rust server” blindly.

Done when:

- RustIQ has a real staged path to a large Rust directory.

## Phase 12: Verification Checklist

Before moving beyond this plan, verify:

1. Start PostgreSQL:

```powershell
docker compose up -d postgres
```

2. Start the API:

```powershell
dotnet run --project .\backend\src\RustIQ.Api --launch-profile http
```

3. Confirm discovery/import works:

- RustIQ ingests Rust servers from BattleMetrics
- stored rows appear in PostgreSQL

4. Confirm cached APIs work:

```http
GET /servers
GET /servers/{id}
GET /servers/{id}/players
GET /servers/{id}/map
GET /servers/{id}/snapshots
```

5. Confirm summary data is coming from stored ingested data, not page-request live querying.

6. Confirm A2S player-list enrichment failure does not break server summary correctness.

7. Confirm SignalR still works for watched server pages using RustIQ’s cached/live update model.

This phase is done when:

- RustIQ can ingest, store, refresh, and serve Rust server summaries at scale.

## Phase 13: What Not To Build Yet

Still do not build these yet:

- Public accounts
- Admin dashboard UI
- Billing
- Full RCON fleet management
- Analytics warehouse
- Kubernetes/serverless rewrite
- Content systems
- Manual hand-curated per-server override workflows as the main scaling solution

The backend should now answer this question:

> Can RustIQ ingest and serve a large Rust server directory using BattleMetrics as the upstream summary source while keeping RustIQ’s own database and API as the product source of truth?
