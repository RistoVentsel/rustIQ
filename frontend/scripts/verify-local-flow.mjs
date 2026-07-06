import { HubConnectionBuilder } from '@microsoft/signalr'

const apiBaseUrl = process.env.RUSTIQ_API_BASE_URL ?? 'http://localhost:5009'
const frontendOrigin = process.env.RUSTIQ_FRONTEND_ORIGIN ?? 'http://127.0.0.1:5173'
const signalrTimeoutMs = Number(process.env.RUSTIQ_SIGNALR_TIMEOUT_MS ?? '20000')

async function fetchJson(path, init = {}) {
  const response = await fetch(new URL(path, `${apiBaseUrl}/`), init)

  if (!response.ok) {
    throw new Error(`Request failed for ${response.url}: ${response.status} ${response.statusText}`)
  }

  return response.json()
}

async function verifyCors() {
  const response = await fetch(new URL('/servers', `${apiBaseUrl}/`), {
    headers: {
      Origin: frontendOrigin,
    },
  })

  const allowOrigin = response.headers.get('access-control-allow-origin')

  if (allowOrigin !== frontendOrigin) {
    throw new Error(
      `Expected CORS allow-origin ${frontendOrigin}, received ${allowOrigin ?? 'null'}.`,
    )
  }

  return allowOrigin
}

async function waitForSignalrUpdate(serverId) {
  const events = []
  const connection = new HubConnectionBuilder()
    .withUrl(new URL('/hubs/servers', `${apiBaseUrl}/`).toString())
    .withAutomaticReconnect()
    .build()

  connection.on('ServerUpdated', (update) => {
    if (update.serverId === serverId) {
      events.push(update)
    }
  })

  await connection.start()

  try {
    await connection.invoke('WatchServer', serverId)

    const deadline = Date.now() + signalrTimeoutMs
    while (events.length === 0 && Date.now() < deadline) {
      await new Promise((resolve) => setTimeout(resolve, 250))
    }

    if (events.length === 0) {
      throw new Error(`No ServerUpdated event received within ${signalrTimeoutMs}ms.`)
    }

    return events[0]
  } finally {
    try {
      await connection.invoke('UnwatchServer', serverId)
    } catch {
      // Ignore cleanup failures after verification.
    }

    await connection.stop()
  }
}

function ensure(condition, message) {
  if (!condition) {
    throw new Error(message)
  }
}

async function main() {
  console.log(`Verifying RustIQ local flow against ${apiBaseUrl}`)
  console.log(`Expecting frontend origin ${frontendOrigin}`)

  const allowOrigin = await verifyCors()
  console.log(`PASS CORS allows ${allowOrigin}`)

  const list = await fetchJson('/servers')
  ensure(Array.isArray(list.items), 'GET /servers did not return a paged items array.')
  ensure(list.items.length > 0, 'GET /servers returned no cached servers to verify.')
  const server = list.items[0]
  console.log(`PASS list page data available for ${server.name ?? server.id}`)

  const detail = await fetchJson(`/servers/${server.id}`)
  ensure(detail.id === server.id, 'GET /servers/{id} returned the wrong server.')
  console.log(`PASS detail endpoint returned ${detail.players}/${detail.maxPlayers}`)

  const players = await fetchJson(`/servers/${server.id}/players`)
  ensure(Array.isArray(players.players), 'GET /servers/{id}/players did not return a player array.')
  console.log(
    `PASS players endpoint returned ${players.players.length} players (refreshed ${players.refreshedAtUtc ?? 'unavailable'})`,
  )

  const map = await fetchJson(`/servers/${server.id}/map`)
  if (map === null) {
    console.log('PASS map endpoint returned null cleanly')
  } else {
    console.log(`PASS map endpoint returned ${map.mapName ?? 'unnamed map'} (${map.source})`)
  }

  const snapshots = await fetchJson(`/servers/${server.id}/snapshots?limit=5`)
  ensure(Array.isArray(snapshots), 'GET /servers/{id}/snapshots did not return an array.')
  console.log(`PASS snapshots endpoint returned ${snapshots.length} recent entries`)

  const liveUpdate = await waitForSignalrUpdate(server.id)
  console.log(
    `PASS SignalR update received: ${liveUpdate.players}/${liveUpdate.maxPlayers} at ${liveUpdate.checkedAtUtc}`,
  )

  console.log('Verification complete.')
}

main().catch((error) => {
  console.error(`FAIL ${error instanceof Error ? error.message : String(error)}`)
  process.exitCode = 1
})
