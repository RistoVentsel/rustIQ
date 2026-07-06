# RustIQ Frontend Step-by-Step Plan

This plan focuses only on the frontend.

The backend proof already works:

- One Rust server is polled repeatedly.
- Cached REST endpoints exist.
- SignalR live updates exist.
- PostgreSQL persistence works.

The first frontend goal should stay small:

> Open one RustIQ server page and watch the player count update live.

Do not start with a huge server directory UI, account system, or marketing site. Build the useful app screen first.

## Frontend Scope for the MVP

For the MVP frontend, build only what is needed to prove the user experience:

- Load cached server data from the backend.
- Show one server detail page clearly.
- Open a SignalR connection for that specific server page.
- Push live player count updates into the UI.
- Show current players when available.
- Show map metadata when available.

Do not build these yet:

- Authentication.
- Admin pages.
- Global live-refreshing server tables.
- Full analytics dashboards.
- Complex chart tooling beyond a simple history view.
- Daily minigames or content sections.
- Multi-server discovery UX.

## Current Backend Contract

The frontend should use these existing backend endpoints:

```http
GET /servers
GET /servers/{id}
GET /servers/{id}/snapshots
GET /servers/{id}/players
GET /servers/{id}/map
```

And this existing SignalR hub:

```http
/hubs/servers
```

Hub methods:

```csharp
Task WatchServer(Guid serverId)
Task UnwatchServer(Guid serverId)
```

Event to listen for:

- `ServerUpdated`

Important product behavior:

- The server list page is a cached snapshot, not a live table.
- The server detail page is where the live experience should happen.
- If multiple users view the same server, the backend queries once and broadcasts to all viewers.

## Recommended Frontend Stack

Use:

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- SignalR JavaScript client

You can add a chart library later for snapshots. Do not block the first live page on chart choices.

## Phase 1: Scaffold the Frontend App

Work from the repository root:

```powershell
cd C:\Users\test\rustIQ
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
```

Add the core packages:

```powershell
npm install react-router-dom @tanstack/react-query @microsoft/signalr
```

Done when:

- `frontend/` contains a working React + TypeScript + Vite app.
- `npm run dev` starts locally.

## Phase 2: Set Up App Structure

Create a small structure that stays easy to grow:

```text
frontend/
  src/
    api/
    app/
    components/
    features/
      servers/
    hooks/
    lib/
    pages/
    routes/
    styles/
```

Suggested responsibilities:

- `api/`: fetch functions for REST endpoints.
- `app/`: app shell, providers, query client.
- `components/`: small reusable UI pieces.
- `features/servers/`: server-specific UI and logic.
- `pages/`: route-level screens.
- `routes/`: router definitions.
- `styles/`: global CSS variables and base styles.

Done when:

- The app has a clean shell and provider setup.
- Routing and TanStack Query are configured.

## Phase 3: Add Frontend Configuration

Use a Vite environment variable for the API base URL.

Create:

```text
frontend/.env.development
```

Example:

```env
VITE_API_BASE_URL=http://localhost:5009
```

If needed later, derive the hub URL from the same base URL.

Done when:

- The app can call the backend without hardcoding URLs in components.

## Phase 4: Build the API Client Layer

Create typed API helpers for:

- `getServers`
- `getServerById`
- `getServerSnapshots`
- `getServerPlayers`
- `getServerMap`

Also define frontend types that match the backend DTOs.

Implementation rules:

- Keep fetch code out of page components.
- Handle non-200 responses cleanly.
- Keep the response types narrow and explicit.

Done when:

- Components can load backend data through a small typed client layer.

## Phase 5: Build the First Routes

Add these initial routes:

```text
/
/servers/:serverId
```

Behavior:

- `/` should load the server list from `GET /servers`.
- Clicking a server row/card should navigate to `/servers/:serverId`.
- If there is only one server, the homepage can still show a compact list and make the detail page the main experience.

Done when:

- A user can open the app and navigate to a specific server page.

## Phase 6: Build the Server List Page

The server list page should be fast and cached, not live.

Show:

- Server name
- Host and query port if useful
- Online/offline status
- Players / max players
- Map name
- Updated seconds ago

Behavior:

- Load from `GET /servers`
- Refresh on page load
- Refresh on user interaction later
- Do not open SignalR for every row

For the first pass:

- Keep layout simple and readable.
- No auto-refresh timer.
- No full advanced filtering UI yet.

Done when:

- The list page correctly renders the stored server data from the backend.

## Phase 7: Build the Server Detail Page

This is the most important MVP page.

Load on page open:

- `GET /servers/{id}`
- `GET /servers/{id}/players`
- `GET /servers/{id}/map`

Optional on first pass:

- `GET /servers/{id}/snapshots`

Show:

- Server name
- Online/offline status
- Players / max players
- Updated seconds ago
- Last seen seconds ago
- Map name
- Seed
- Size
- RustMaps link
- Thumbnail image if available
- Current player list if available

Fallback behavior:

- If players list is empty, show a clear unavailable or empty state.
- If thumbnail is missing, show text-only map metadata.
- If map metadata is missing, do not show a broken section.

Done when:

- The detail page is useful even before live updates are attached.

## Phase 8: Add SignalR Live Updates

On the server detail page:

1. Connect to `/hubs/servers`
2. Call `WatchServer(serverId)` when the page opens
3. Listen for `ServerUpdated`
4. Update the displayed live fields immediately
5. Call `UnwatchServer(serverId)` when leaving the page

Live fields to update:

- Players
- Max players
- Status
- Updated seconds ago
- Map summary fields if included

Rules:

- Only the detail page should open the live connection.
- Do not open a hub connection for the list page.
- Keep the UI responsive if the SignalR connection drops and reconnects.

Done when:

- Opening a server page shows live player count changes without page refresh.

## Phase 9: Add Updated-Time UX

The product context explicitly wants:

- `updated Xs ago`

Implement a small frontend timer that updates the displayed relative age locally.

Rules:

- Do not refetch just to update the text.
- Recompute the label in the browser every second.

Done when:

- The page visibly feels live even between pushes.

## Phase 10: Add Snapshot History View

Use `GET /servers/{id}/snapshots`.

First pass options:

- Simple table of recent snapshots
- Or a minimal line chart if a chart library is added

For the MVP:

- Keep it simple.
- Focus on recent player history only.
- Do not overbuild analytics.

Done when:

- The detail page can show recent player-count history from cached snapshot data.

## Phase 11: Add Search and Pagination to the List Page

After the first live detail page works, expand the list page.

Use `GET /servers` with:

- `search`
- `status`
- `page`
- `pageSize`

Behavior:

- Fetch on search submit or debounced input
- Fetch on status change
- Fetch on page change
- Keep the list cached between navigations when practical

Do not add live row updates here.

Done when:

- The list page behaves like a fast cached Rust server snapshot.

## Phase 12: Add Basic Frontend States

Handle:

- Loading states
- Error states
- Empty states
- Reconnecting SignalR state
- Missing player list state
- Missing map metadata state

Do not leave blank panels or raw JSON-looking failures.

Done when:

- The UI behaves predictably when backend data is slow, missing, or temporarily unavailable.

## Phase 13: Local Verification Checklist

Before thinking about deployment, verify this flow locally:

1. Start PostgreSQL:

```powershell
docker compose up -d postgres
```

2. Start the backend:

```powershell
dotnet run --project .\backend\src\RustIQ.Api --launch-profile http
```

3. Start the frontend:

```powershell
cd .\frontend
npm run dev
```

4. Open the frontend in the browser.

5. Confirm the list page loads cached server data.

6. Open a server detail page.

7. Confirm it loads:

- server info
- current players
- map metadata
- snapshot history if built

8. Confirm SignalR works:

- the page subscribes to that server
- player count changes arrive live
- `updated Xs ago` changes correctly

Frontend MVP is done when:

- The app loads locally.
- One server can be viewed from the UI.
- The server detail page updates live without refresh.
- Cached data loads immediately before live updates arrive.
- Missing players or map metadata are handled cleanly.

## Phase 14: What Not To Build Yet

Do not build these before the first live frontend proof works:

- Marketing homepage first
- Authentication
- Admin tools
- Full global real-time server table
- Discovery UI for every Rust server on the internet
- Heavy analytics dashboards
- Long-term player tracking UI
- VPS deployment work
- Domain setup
- Content/minigame sections

The frontend should first answer one question:

> Can a user open one Rust server page and clearly see live player count updates in a fast UI?
