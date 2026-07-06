# RustIQ Backend Step-by-Step Plan

This plan focuses only on the backend. The goal is to build the smallest useful backend proof first:

> Query one Rust server, return its live player count and map metadata through an API, then push updates through SignalR.

Frontend work, branding, domain buying, VPS setup, and UI design are intentionally out of scope for this document.

## Phase 0: Architecture and Backend Scope

Use a clean onion-style backend, not a flat single-project API and not old-school `BLL` / `DAL` folder naming.

For RustIQ, clean onion is the better choice because the backend will quickly have several separate concerns:

- Rust server query protocol code.
- Database persistence.
- Background polling.
- REST API endpoints.
- SignalR live updates.
- Rust map metadata enrichment, including seed, size, RustMaps link, and map thumbnail.
- Business rules for online/offline status and snapshots.

The project should use these layers:

```text
backend/
  RustIQ.sln
  src/
    RustIQ.Api/
    RustIQ.Application/
    RustIQ.Domain/
    RustIQ.Infrastructure/
  tests/
    RustIQ.Tests/
```

Layer responsibilities:

- `RustIQ.Domain`: core entities, enums, and domain rules. This is the cleanest part of the app and should not depend on ASP.NET Core, EF Core, PostgreSQL, SignalR, or Steam query packages.
- `RustIQ.Application`: use cases, service interfaces, DTOs, and business workflow logic. This is the modern equivalent of a `BLL` layer.
- `RustIQ.Infrastructure`: EF Core database access, repository implementations, PostgreSQL setup, Steam server query implementation, and external service code. This is the modern equivalent of a `DAL` layer.
- `RustIQ.Api`: ASP.NET Core startup, dependency injection, REST endpoints, SignalR hubs, hosted workers, configuration, and HTTP-facing request/response models.
- `RustIQ.Tests`: tests for domain rules, application services, and API behavior.

Dependency direction:

```text
RustIQ.Api            -> RustIQ.Application, RustIQ.Infrastructure
RustIQ.Infrastructure -> RustIQ.Application, RustIQ.Domain
RustIQ.Application    -> RustIQ.Domain
RustIQ.Domain         -> no RustIQ project dependencies
```

Do not put business logic directly in controllers or SignalR hubs. Controllers and hubs should call application services. Do not put EF Core or PostgreSQL code in `Domain` or `Application`; keep that in `Infrastructure`.

Build the backend in this order:

1. Create the clean onion solution structure.
2. Add a Steam server query service behind an application interface.
3. Query one hardcoded Rust server.
4. Expose one test API endpoint.
5. Add Rust map metadata enrichment for the configured server.
6. Add PostgreSQL.
7. Store latest server state, map metadata, and snapshots.
8. Add background polling.
9. Add normal REST endpoints.
10. Add SignalR live updates for one watched server.
11. Add basic tests and local run instructions.

Keep the architecture clean, but keep the implementation small. The goal is not enterprise complexity; the goal is to avoid mixing database code, Steam query code, business rules, and API endpoints into one messy project.

## Phase 1: Create the Backend Solution Structure

Work from the repository root:

```powershell
cd C:\Users\test\rustIQ
cd backend
dotnet new sln -n RustIQ
mkdir src
mkdir tests
dotnet new classlib -n RustIQ.Domain -o .\src\RustIQ.Domain
dotnet new classlib -n RustIQ.Application -o .\src\RustIQ.Application
dotnet new classlib -n RustIQ.Infrastructure -o .\src\RustIQ.Infrastructure
dotnet new webapi -n RustIQ.Api -o .\src\RustIQ.Api
dotnet sln add .\src\RustIQ.Domain\RustIQ.Domain.csproj
dotnet sln add .\src\RustIQ.Application\RustIQ.Application.csproj
dotnet sln add .\src\RustIQ.Infrastructure\RustIQ.Infrastructure.csproj
dotnet sln add .\src\RustIQ.Api\RustIQ.Api.csproj
dotnet add .\src\RustIQ.Application reference .\src\RustIQ.Domain
dotnet add .\src\RustIQ.Infrastructure reference .\src\RustIQ.Application
dotnet add .\src\RustIQ.Infrastructure reference .\src\RustIQ.Domain
dotnet add .\src\RustIQ.Api reference .\src\RustIQ.Application
dotnet add .\src\RustIQ.Api reference .\src\RustIQ.Infrastructure
```

Expected structure after this phase:

```text
backend/
  RustIQ.sln
  src/
    RustIQ.Api/
      RustIQ.Api.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
    RustIQ.Application/
      RustIQ.Application.csproj
    RustIQ.Domain/
      RustIQ.Domain.csproj
    RustIQ.Infrastructure/
      RustIQ.Infrastructure.csproj
  tests/
```

Recommended folder layout as the backend grows:

```text
backend/
  src/
    RustIQ.Api/
      Endpoints/
      Realtime/
      Workers/
      Program.cs
      appsettings.json
      appsettings.Development.json
    RustIQ.Application/
      Maps/
      Realtime/
      ServerQuery/
      Servers/
    RustIQ.Domain/
      Maps/
      Servers/
    RustIQ.Infrastructure/
      Data/
      DependencyInjection.cs
      Maps/
      ServerQuery/
      Servers/
  tests/
    RustIQ.Tests/
```

Use `Application` instead of a `BLL` directory. Use `Infrastructure` instead of a `DAL` directory. The responsibilities are the same idea, but the names are cleaner and more common in modern .NET projects.

Then run:

```powershell
dotnet run --project .\src\RustIQ.Api
```

Confirm the default API starts locally. If HTTPS causes friction during early testing, use the HTTP URL shown by `dotnet run`.

Done when:

- `dotnet build` succeeds.
- The API starts locally.
- Swagger or the default OpenAPI endpoint loads.

## Phase 2: Decide the First Rust Server to Query

Pick one known Rust server and keep it configurable. Do not hardcode it directly inside the query logic.

Add this to `src/RustIQ.Api/appsettings.Development.json`:

```json
{
  "RustServerQuery": {
    "Host": "CHANGE_ME",
    "QueryPort": 28015,
    "TimeoutMilliseconds": 3000
  }
}
```

Important:

- Rust game ports and query ports can differ by server.
- Start with one server that is known to be online.
- Keep timeout short. A slow or dead server should not hang the API.

Done when:

- The selected server host and query port are stored in configuration.
- The backend can read these values using the options pattern.

## Phase 3: Add Steam Server Query Support

The backend needs to speak the Steam server query protocol. Do not write low-level UDP parsing first unless no package works.

First try a NuGet package that supports A2S server info and player queries. Search for packages around:

- `A2S`
- `SteamQuery`
- `SteamServerQuery`
- `SourceQuery`

Example install command, using the package that you verify works:

```powershell
dotnet add .\src\RustIQ.Infrastructure package PACKAGE_NAME
```

Create these backend types:

```text
RustIQ.Application/
  ServerQuery/
    RustServerQueryOptions.cs
    RustServerQueryResult.cs
    RustServerPlayerResult.cs
    IRustServerQueryClient.cs
RustIQ.Infrastructure/
  ServerQuery/
    SteamRustServerQueryClient.cs
```

`RustServerQueryOptions` should contain:

- `Host`
- `QueryPort`
- `TimeoutMilliseconds`

`RustServerQueryResult` should contain:

- `Host`
- `QueryPort`
- `Name`
- `Map`
- `Players`
- `MaxPlayers`
- `IsOnline`
- `PingMilliseconds`
- `CheckedAtUtc`
- `Error`

`RustServerPlayerResult` should contain:

- `Name`
- `Score`
- `DurationSeconds`

`IRustServerQueryClient` should expose:

```csharp
Task<RustServerQueryResult> QueryServerAsync(CancellationToken cancellationToken);
Task<IReadOnlyList<RustServerPlayerResult>> QueryPlayersAsync(CancellationToken cancellationToken);
```

Implementation rules:

- Catch query failures and return `IsOnline = false` instead of crashing the API.
- Include the error message internally for debugging.
- Use UTC timestamps only.
- Treat player names as optional because some Rust servers may not return them.
- Keep the Steam query NuGet package inside `RustIQ.Infrastructure`.
- Keep `IRustServerQueryClient` inside `RustIQ.Application` so application services depend on an interface, not on a specific Steam query package.

Done when:

- The query client can ask one configured server for name, player count, max players, and map if available.
- Failed queries return a controlled offline result.

## Phase 4: Add the First Test Endpoint

Add a temporary proof endpoint:

```http
GET /debug/query-server
```

It should:

1. Call `IRustServerQueryClient.QueryServerAsync`.
2. Return the query result as JSON.
3. Not use the database yet.

Example response:

```json
{
  "host": "example.server.host",
  "queryPort": 28015,
  "name": "Rust Server Name",
  "map": "Procedural Map",
  "players": 123,
  "maxPlayers": 250,
  "isOnline": true,
  "pingMilliseconds": 42,
  "checkedAtUtc": "2026-07-06T12:00:00Z",
  "error": null
}
```

Also add:

```http
GET /debug/query-players
```

It should:

1. Call `IRustServerQueryClient.QueryPlayersAsync`.
2. Return player names if available.
3. Return an empty list or a clear unavailable state if the query fails.

Done when:

- `/debug/query-server` returns live data from one Rust server.
- `/debug/query-players` either returns current players or fails gracefully.
- `dotnet build` still succeeds.

## Phase 5: Add Rust Map Metadata Support

BattleMetrics shows useful map information for Rust servers, including seed, size, map name, a map preview image, and a RustMaps link. RustIQ should support a similar backend data shape, but do not include entity count or monument count in the MVP.

The first backend implementation should treat map data as metadata enrichment separate from the Steam A2S query:

- Steam A2S query remains responsible for live server name, map name, player count, max players, and online/offline status.
- Map metadata enrichment is responsible for Rust-specific map details such as seed, world size, RustMaps URL, thumbnail URL, and source update time.

Create these backend types:

```text
RustIQ.Application/
  Maps/
    RustServerMapMetadata.cs
    IRustServerMapMetadataClient.cs
RustIQ.Infrastructure/
  Maps/
    BattleMetricsRustServerMapMetadataClient.cs
```

`RustServerMapMetadata` should contain:

- `Host`
- `QueryPort`
- `MapName`
- `Seed`
- `Size`
- `RustMapsUrl`
- `ThumbnailUrl`
- `MapFileUrl`
- `Source`
- `SourceUpdatedAtUtc`
- `FetchedAtUtc`
- `Error`

`IRustServerMapMetadataClient` should expose:

```csharp
Task<RustServerMapMetadata> GetMapMetadataAsync(CancellationToken cancellationToken);
```

Initial implementation approach:

1. Use the configured server host and query port.
2. Call the public BattleMetrics API to find the matching Rust server.
3. Read map values from the Rust server details if available.
4. Return a controlled result with `Error` populated if map metadata is unavailable.

The useful BattleMetrics/RustMaps fields are:

- `details.map`
- `details.rust_world_seed`
- `details.rust_world_size`
- `details.rust_maps.url`
- `details.rust_maps.thumbnailUrl`
- `details.rust_maps.mapUrl`
- `details.rust_maps.updatedAt`

Do not store or expose these in the MVP:

- Entity count.
- Monument count.
- Full monument list.

Also add a temporary proof endpoint:

```http
GET /debug/query-map
```

It should:

1. Call `IRustServerMapMetadataClient.GetMapMetadataAsync`.
2. Return the map metadata as JSON.
3. Not use the database yet.

Example response:

```json
{
  "host": "example.server.host",
  "queryPort": 28015,
  "mapName": "Moose Maps",
  "seed": 1337,
  "size": 4500,
  "rustMapsUrl": "https://rustmaps.com/map/9cf336e898d94633920c5ecf2da9afc9",
  "thumbnailUrl": "https://content.rustmaps.com/maps/286/9cf336e898d94633920c5ecf2da9afc9/thumbnail.webp",
  "mapFileUrl": "https://maps.rustmaps.com/286/9cf336e898d94633920c5ecf2da9afc9/4500_example.map",
  "source": "BattleMetrics",
  "sourceUpdatedAtUtc": "2026-07-06T14:21:07.995Z",
  "fetchedAtUtc": "2026-07-06T15:00:00Z",
  "error": null
}
```

Frontend note for later:

- Show seed, size, map name, and the map thumbnail.
- Make the thumbnail and/or a text link open `rustMapsUrl`.
- Do not show entity count or monument count.
- If thumbnail is unavailable, show the map name and RustMaps link without a broken image.

Done when:

- `/debug/query-map` returns map metadata for the configured server when available.
- Missing metadata returns a controlled response instead of crashing the API.
- `dotnet build` still succeeds.

## Phase 6: Add PostgreSQL Locally

Add a `docker-compose.yml` at the repository root, not inside `backend/`, because it will eventually serve both backend and frontend development:

```yaml
services:
  postgres:
    image: postgres:16
    container_name: rustiq-postgres
    environment:
      POSTGRES_DB: rustiq
      POSTGRES_USER: rustiq
      POSTGRES_PASSWORD: rustiq_dev_password
    ports:
      - "5432:5432"
    volumes:
      - rustiq-postgres-data:/var/lib/postgresql/data

volumes:
  rustiq-postgres-data:
```

Start it:

```powershell
docker compose up -d postgres
```

Add EF Core packages:

```powershell
dotnet add .\backend\src\RustIQ.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add .\backend\src\RustIQ.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add .\backend\src\RustIQ.Infrastructure package Microsoft.EntityFrameworkCore.Design
```

Add connection string to `backend/src/RustIQ.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=rustiq;Username=rustiq;Password=rustiq_dev_password"
  }
}
```

If host port `5432` is already occupied locally, keep the container port as `5432` and use another host port such as `5433`:

```yaml
ports:
  - "5433:5432"
```

Then set the development connection string to:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=rustiq;Username=rustiq;Password=rustiq_dev_password"
  }
}
```

Done when:

- PostgreSQL runs in Docker.
- The API can connect to PostgreSQL.
- EF Core is registered through an infrastructure dependency injection method, then called from `RustIQ.Api/Program.cs`.

## Phase 7: Create the First Data Model

Create:

```text
RustIQ.Domain/
  Servers/
    Server.cs
    ServerSnapshot.cs
    ServerStatus.cs
    ServerPlayerCurrent.cs
    ServerMapMetadata.cs
RustIQ.Infrastructure/
  Data/
    RustIqDbContext.cs
    EntityTypeConfigurations if needed later
```

`Server` fields:

- `Id`
- `Host`
- `QueryPort`
- `Name`
- `Map`
- `Players`
- `MaxPlayers`
- `Status`
- `LastSeenAtUtc`
- `LastCheckedAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

`ServerSnapshot` fields:

- `Id`
- `ServerId`
- `CheckedAtUtc`
- `Players`
- `MaxPlayers`
- `IsOnline`
- `PingMilliseconds`

`ServerPlayerCurrent` fields:

- `Id`
- `ServerId`
- `PlayerName`
- `Score`
- `DurationSeconds`
- `SeenAtUtc`

`ServerMapMetadata` fields:

- `Id`
- `ServerId`
- `MapName`
- `Seed`
- `Size`
- `RustMapsUrl`
- `ThumbnailUrl`
- `MapFileUrl`
- `Source`
- `SourceUpdatedAtUtc`
- `FetchedAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

`ServerStatus` values:

- `Online`
- `Offline`
- `Unknown`

Important database rules:

- Store latest server state in `servers`.
- Store latest map metadata in `server_map_metadata`.
- Store time-series rows in `server_snapshots`.
- Store current player names only as temporary current state.
- Do not delete a server just because it goes offline.
- Do not permanently track player history in the MVP.
- Do not store entity count, monument count, or the full monument list in the MVP.

Create the first migration:

```powershell
dotnet ef migrations add InitialCreate --project .\backend\src\RustIQ.Infrastructure --startup-project .\backend\src\RustIQ.Api
dotnet ef database update --project .\backend\src\RustIQ.Infrastructure --startup-project .\backend\src\RustIQ.Api
```

Done when:

- Database tables exist.
- The model can store one server, map metadata, and snapshots.

## Phase 8: Save Query Results

Create:

```text
RustIQ.Application/
  Servers/
    IServerRepository.cs
    ServerPollingService.cs
    ServerDto.cs
    ServerSnapshotDto.cs
    CurrentPlayerDto.cs
    ServerMapMetadataDto.cs
RustIQ.Infrastructure/
  Servers/
    ServerRepository.cs
```

`ServerPollingService` should:

1. Query the configured Rust server.
2. Find or create a `Server` row by `Host` and `QueryPort`.
3. Update the latest server state.
4. Query and update map metadata when available.
5. Insert a `ServerSnapshot` row.
6. If player names are available, replace current players for that server.
7. Save changes.

Offline behavior:

- If one query fails, update `LastCheckedAtUtc`.
- Do not immediately erase `LastSeenAtUtc`.
- Mark offline only after repeated failures or after `LastSeenAtUtc` is older than a chosen threshold, such as 60 seconds for the MVP.

Done when:

- Calling a service method once stores or updates one server.
- Map metadata is stored or updated for that server when available.
- A snapshot row is added each time the query runs.
- Offline query failures do not delete data.

## Phase 9: Add Background Polling

Create a hosted service:

```text
RustIQ.Api/
  Workers/
    ServerPollingWorker.cs
```

The first worker can poll only the configured server.

Initial polling intervals:

- Server info and player count: every 5 seconds.
- Current player names: every 30 seconds.
- Map metadata refresh: every 5-15 minutes, because seed/size/map image changes much less often than player counts.

Implementation notes:

- Use `BackgroundService`.
- Use `PeriodicTimer`.
- Create a scoped service provider inside each polling cycle.
- Log failures without crashing the process.
- Do not allow overlapping polls. If one poll is still running, skip the next tick.

Done when:

- Starting the API automatically polls the configured server.
- The database receives snapshots every few seconds.
- The database refreshes map metadata periodically without querying it every 5 seconds.
- Logs show successful query checks or controlled failures.

## Phase 10: Add Backend REST Endpoints

Replace temporary debug-only behavior with real read endpoints:

```http
GET /servers
GET /servers/{id}
GET /servers/{id}/snapshots
GET /servers/{id}/players
GET /servers/{id}/map
```

`GET /servers` should support:

- `search`
- `status`
- `page`
- `pageSize`

For the MVP:

- Sort online servers first.
- Then sort by players descending.
- Limit `pageSize` to a reasonable maximum, such as 100.

`GET /servers/{id}` should return:

- Latest known server data.
- `updatedSecondsAgo`.
- `lastSeenSecondsAgo`.
- Summary map metadata if available, including map name, seed, size, RustMaps URL, and thumbnail URL.

`GET /servers/{id}/snapshots` should support:

- `from`
- `to`
- `limit`

For the MVP, return the latest 100-500 snapshots if no range is supplied.

`GET /servers/{id}/players` should return:

- Current player names if available.
- A timestamp showing when they were last refreshed.

`GET /servers/{id}/map` should return:

- Map name.
- Seed.
- Size.
- RustMaps URL.
- Thumbnail URL.
- Map file URL if available.
- Source update timestamp.
- Fetch timestamp.

Done when:

- The API can serve stored server data without querying the Rust server during each request.
- Server list requests are database reads only.
- Detail page requests return cached data quickly.
- Map metadata requests are database reads only.

## Phase 11: Add SignalR Live Server Updates

ASP.NET Core Web API projects already use the ASP.NET Core shared framework, so a separate SignalR server package is usually not needed. If the compiler cannot resolve SignalR types, then add the package to the API project:

```powershell
dotnet add .\backend\src\RustIQ.Api package Microsoft.AspNetCore.SignalR
```

Create:

```text
RustIQ.Api/
  Realtime/
    ServerUpdatesHub.cs
RustIQ.Application/
  Realtime/
    ServerUpdateMessage.cs
```

Map the hub:

```http
/hubs/servers
```

Hub methods:

```csharp
Task WatchServer(Guid serverId);
Task UnwatchServer(Guid serverId);
```

Use SignalR groups:

- Group name: `server:{serverId}`
- When a client watches a server, add it to that server group.
- When a client stops watching, remove it from that group.

When `ServerPollingWorker` stores a new result:

1. Build a `ServerUpdateMessage`.
2. Send it to `server:{serverId}`.
3. Include player count, max players, status, checked time, and updated seconds ago.
4. Include map name, seed, size, RustMaps URL, and thumbnail URL when available.

Important:

- Do not make every client query the Rust server.
- The worker queries the Rust server once.
- SignalR broadcasts the result to all clients watching that server.

Done when:

- A SignalR client can connect to `/hubs/servers`.
- A client can call `WatchServer(serverId)`.
- The backend broadcasts updated player counts every few seconds.
- The backend can include map metadata in update messages without requiring clients to call BattleMetrics or RustMaps directly.

## Phase 12: Add Basic Tests

Create a test project:

```powershell
cd C:\Users\test\rustIQ\backend
dotnet new xunit -n RustIQ.Tests -o .\tests\RustIQ.Tests
dotnet sln add .\tests\RustIQ.Tests\RustIQ.Tests.csproj
dotnet add .\tests\RustIQ.Tests reference .\src\RustIQ.Domain\RustIQ.Domain.csproj
dotnet add .\tests\RustIQ.Tests reference .\src\RustIQ.Application\RustIQ.Application.csproj
dotnet add .\tests\RustIQ.Tests reference .\src\RustIQ.Infrastructure\RustIQ.Infrastructure.csproj
dotnet add .\tests\RustIQ.Tests reference .\src\RustIQ.Api\RustIQ.Api.csproj
```

Test the parts that do not require a real Rust server first:

- Mapping query results into `Server`.
- Snapshot creation.
- Map metadata mapping and fallback behavior.
- Offline threshold behavior.
- Page size clamping for `GET /servers`.
- SignalR message shape.

Avoid tests that depend on a real public Rust server being online. For query behavior, use a fake `IRustServerQueryClient`.

Done when:

- `dotnet test` passes.
- The important offline, snapshot, and map metadata rules are covered.

## Phase 13: Backend Verification Checklist

Before starting frontend work, the backend should prove this flow:

1. Start PostgreSQL:

```powershell
docker compose up -d postgres
```

2. Start the API:

```powershell
dotnet run --project .\backend\src\RustIQ.Api
```

3. Confirm the debug query endpoint works:

```http
GET /debug/query-server
GET /debug/query-map
```

4. Confirm the database contains:

- One server row.
- One map metadata row when metadata is available.
- Repeated snapshot rows.

5. Confirm REST endpoints work:

```http
GET /servers
GET /servers/{id}
GET /servers/{id}/snapshots
GET /servers/{id}/players
GET /servers/{id}/map
```

6. Confirm SignalR broadcasts:

- Connect to `/hubs/servers`.
- Call `WatchServer(serverId)`.
- Watch messages arrive every few seconds.

Backend MVP is done when:

- One Rust server is polled repeatedly.
- Player count and max players are stored.
- Map name, seed, size, RustMaps URL, and thumbnail URL are stored when available.
- Snapshots are saved.
- REST endpoints return cached data.
- SignalR pushes live updates for that one server.
- Failed Rust server queries are handled without crashing the API.
- Missing map metadata is handled without crashing the API.

## Phase 14: What Not To Build Yet

Do not build these until the backend proof works:

- Full Rust server discovery.
- Huge server list crawling.
- Authentication.
- Admin dashboard.
- Paid hosting.
- Cloudflare deployment.
- Domain setup.
- Long-term player tracking.
- Complex analytics.
- Data compression jobs.
- Kubernetes or serverless infrastructure.
- Full monument analytics or monument search.
- Entity count tracking.

The backend should first answer one question:

> Can RustIQ reliably query one Rust server, store the result, and push live player-count updates?
