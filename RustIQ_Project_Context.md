# RustIQ Project Context

## Project Idea

RustIQ is planned as a Rust-only server intelligence website, similar in spirit to BattleMetrics but narrower, faster, and more focused on Rust servers.

The name being considered is `rustIQ.gg`. The `.gg` domain was found for about 45 EUR, which is acceptable, but the domain does not need to be bought before development starts.

The site should eventually provide:

- Rust server list
- Server search and filtering
- Online/offline server status
- Player count history
- Live player count on individual server pages
- Current player names on a server, when available
- Server uptime/last-seen history
- Possibly Rust knowledge content
- Possibly daily minigames later

The first goal is not to build everything. The first goal is:

> Open one RustIQ server page and watch the player count update live.

## Important Product Decisions

The default server list page should not constantly live-refresh every row. It should behave like a cached/searchable snapshot.

The server list page should refresh when the user does something interactive:

- Opens the page
- Searches
- Changes filters
- Moves to the next page

The live feeling should exist mainly on a specific server detail page.

When a user opens a specific server page:

- Load cached server info immediately
- Open a live connection to the backend
- Refresh that one server every few seconds
- Push player count updates to the frontend
- Show `updated Xs ago`

If multiple users watch the same server, the backend should query that Rust server once and broadcast the result to all viewers.

## Recommended Stack

Frontend:

- React
- TypeScript
- Vite
- Cloudflare Pages later for hosting
- TanStack Query for normal API calls
- SignalR client for live player count updates
- Chart library later for history graphs

Backend:

- C# / ASP.NET Core
- SignalR for live server detail pages
- BackgroundService / Worker for server polling
- PostgreSQL
- EF Core + Npgsql or Dapper
- Docker Compose for local/dev and VPS deployment

Hosting later:

- Frontend: Cloudflare Pages, probably free
- Backend: cheap VPS
- Database: PostgreSQL on the same VPS at first
- DNS: Cloudflare

Avoid starting with serverless, Kubernetes, managed cloud complexity, or a separate managed database. A cheap VPS is enough for the MVP.

## Why Backend First

Start from the backend because the biggest unknown is not the UI. The biggest unknown is whether the backend can reliably:

- Discover Rust servers
- Query a server
- Read player count
- Read max players
- Read server name
- Read current player names where available
- Store snapshots
- Push live updates

React can come after this proof works.

## Data Model Idea

Basic tables to start:

### servers

Stores the latest known state for each server.

Suggested fields:

- id
- address / ip
- port
- query_port if needed
- name
- map
- players
- max_players
- status: online/offline
- last_seen_at
- last_checked_at
- created_at
- updated_at

### server_snapshots

Stores time-series data.

Suggested fields:

- id
- server_id
- checked_at
- players
- max_players
- is_online
- ping_ms if available

Do not store a full detailed copy of every server every few seconds forever. Keep detailed snapshots for a limited time, then later compress old history into hourly/daily summaries.

### server_players_current

Optional for current player names.

Suggested fields:

- server_id
- player_name
- score if available
- duration_seconds if available
- seen_at

At first, treat player names as temporary current data, not long-term player tracking.

## Online and Offline Servers

RustIQ can store:

- All online servers it can discover
- Offline servers that RustIQ has seen before

It cannot know about a server that went offline before RustIQ ever discovered it.

Offline should mean:

> This server was previously seen by RustIQ, but cannot be reached right now.

Do not delete servers when they go offline. Mark them offline and keep their history.

## Refresh Strategy

Server list page:

- Cached database query
- No automatic live refresh needed
- Refresh when user searches, filters, paginates, or reloads

Specific server detail page:

- Player count: every 3-5 seconds
- Ping/status: every 10-15 seconds
- Player names: every 30-60 seconds
- Server metadata/rules/tags: every few minutes or on page load

If a query fails, do not instantly mark the server offline. Show something like:

> Last seen 18s ago

Then mark offline only after repeated failures or a timeout threshold.

## Player Names

Displaying current player names should usually be possible through Steam server query/player details behavior, but it may not work for every server.

Some servers may:

- Return player names
- Return incomplete player names
- Fail to respond
- Block or rate-limit the query
- Behave inconsistently

The UI should handle this gracefully:

- Show current players when available
- Show `player list unavailable` when not available
- Refresh player names slower than player counts

Avoid permanent BattleMetrics-style player tracking at the start because it adds privacy/GDPR concerns and more storage cost.

## BattleMetrics Slowness Concern

BattleMetrics may feel slow for reasons unrelated to whether live data is possible:

- Heavy pages
- Large tables
- Many network requests
- Ads/trackers
- Big graphs
- Waiting on slow API responses
- Too much frontend work

RustIQ should stay fast by:

- Loading only visible data
- Using cached backend responses
- Avoiding live updates on the whole server list
- Making only server detail pages live
- Using small payloads
- Not forcing full page reloads

## Development Order

Recommended MVP order:

1. Create C# ASP.NET Core backend project.
2. Query one hardcoded Rust server.
3. Return server name, status, player count, max players.
4. Try to return current player names.
5. Add PostgreSQL.
6. Save server and snapshots.
7. Add a background worker/crawler.
8. Add basic API endpoints:
   - `GET /servers`
   - `GET /servers/{id}`
   - `GET /servers/{id}/snapshots`
   - `GET /servers/{id}/players`
9. Add SignalR live updates for one server.
10. Add a tiny React page that displays live player count.
11. Expand to more servers.
12. Add search/filtering.
13. Deploy to VPS when local proof works.
14. Buy/connect domain when ready for public use.

## Repository Structure

Keep frontend and backend in one repo at first.

Suggested structure:

```text
rustIQ/
  backend/
    RustIQ.sln
    src/
      RustIQ.Api/
      RustIQ.Application/
      RustIQ.Infrastructure/
      RustIQ.Domain/
    tests/
      RustIQ.Tests/
  frontend/
    later React app
  docker-compose.yml
  README.md
```

Do not over-engineer too early. If the backend starts simpler with only one API project, that is also fine. The first milestone matters more than perfect architecture.

## Local Development Before Spending Money

No need to buy the domain yet.

No need to buy a VPS yet.

Build locally first:

- Backend on localhost
- PostgreSQL locally or in Docker
- React frontend on localhost
- Test live player count locally

Buy a VPS when:

- The local proof works
- The crawler needs to run 24/7
- The project is ready for private/public testing

Buy the domain when:

- The name is final
- The public version is close
- Or the domain might be taken and the price is acceptable

## Current Recommendation

Start with the backend proof:

> C# API that queries one Rust server and returns live player count.

Once that works, add database snapshots and SignalR. Only after that, create the React frontend.

The first working demo should be small:

- One known Rust server
- One backend endpoint
- One SignalR live feed
- One simple frontend page showing `players/maxPlayers`

Everything else can grow from there.
