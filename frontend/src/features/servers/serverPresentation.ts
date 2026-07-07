import { serverStatus, type ServerDetails, type ServerListItem, type ServerStatus } from '../../api'

export function getServerDisplayName(server: Pick<ServerListItem, 'name' | 'host' | 'queryPort'>) {
  return server.name ?? `${server.host}:${server.queryPort}`
}

export function formatAddress(server: Pick<ServerListItem, 'host' | 'queryPort'>) {
  return `${server.host}:${server.queryPort}`
}

export function formatCountry(country: string | null | undefined) {
  return country?.trim().toUpperCase() || 'Unknown'
}

export function formatServerLocation(
  server: Pick<ServerListItem | ServerDetails, 'country' | 'latitude' | 'longitude'>,
) {
  const country = formatCountry(server.country)
  if (server.latitude === null || server.latitude === undefined
      || server.longitude === null || server.longitude === undefined) {
    return country
  }

  return `${country} (${server.latitude.toFixed(2)}, ${server.longitude.toFixed(2)})`
}

export function formatServerTimestamp(timestamp: string | null | undefined) {
  if (!timestamp) {
    return 'Unavailable'
  }

  const parsed = Date.parse(timestamp)
  if (Number.isNaN(parsed)) {
    return 'Unavailable'
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parsed)
}

export function getServerLastActivityTimestamp(server: Pick<ServerListItem, 'lastCheckedAtUtc' | 'updatedAtUtc'>) {
  return server.lastCheckedAtUtc ?? server.updatedAtUtc
}

export function getServerStatusLabel(status: ServerStatus) {
  switch (status) {
    case serverStatus.Online:
      return 'Online'
    case serverStatus.Offline:
      return 'Offline'
    default:
      return 'Unknown'
  }
}

export function getServerStatusClassName(status: ServerStatus) {
  switch (status) {
    case serverStatus.Online:
      return 'status-pill status-pill--online'
    case serverStatus.Offline:
      return 'status-pill status-pill--offline'
    default:
      return 'status-pill status-pill--unknown'
  }
}

export function formatServerSubtitle(
  server: Pick<ServerListItem, 'host' | 'queryPort' | 'map'> | Pick<ServerDetails, 'host' | 'queryPort' | 'map'>,
) {
  const address = formatAddress(server)
  return server.map ? `${address} | ${server.map}` : address
}
