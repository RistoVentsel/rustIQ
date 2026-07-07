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

export function formatServerLocationShort(
  server: Pick<ServerListItem | ServerDetails, 'country' | 'latitude' | 'longitude'>,
) {
  const flag = getCountryFlag(server.country)
  const country = formatCountry(server.country)
  if (server.latitude === null || server.latitude === undefined
      || server.longitude === null || server.longitude === undefined) {
    return `${flag}${country}`.trim()
  }

  return `${flag}${country} ${server.latitude.toFixed(1)}, ${server.longitude.toFixed(1)}`.trim()
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

export function formatRelativeTimestamp(timestamp: string | null | undefined, nowMs = Date.now()) {
  if (!timestamp) {
    return 'Unknown'
  }

  const parsed = Date.parse(timestamp)
  if (Number.isNaN(parsed)) {
    return 'Unknown'
  }

  const diffSeconds = Math.round((parsed - nowMs) / 1000)
  const absoluteSeconds = Math.abs(diffSeconds)
  const units: Array<[Intl.RelativeTimeFormatUnit, number]> = [
    ['year', 60 * 60 * 24 * 365],
    ['month', 60 * 60 * 24 * 30],
    ['day', 60 * 60 * 24],
    ['hour', 60 * 60],
    ['minute', 60],
  ]
  const [unit, secondsPerUnit] = units.find(([, seconds]) => absoluteSeconds >= seconds) ?? ['second', 1]
  const value = Math.round(diffSeconds / secondsPerUnit)

  return new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' }).format(value, unit)
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

function getCountryFlag(country: string | null | undefined) {
  const code = country?.trim().toUpperCase()
  if (!code || code.length !== 2 || !/^[A-Z]{2}$/.test(code)) {
    return ''
  }

  return `${String.fromCodePoint(0x1f1e6 + code.charCodeAt(0) - 65)}${String.fromCodePoint(
    0x1f1e6 + code.charCodeAt(1) - 65,
  )} `
}
