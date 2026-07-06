import { serverStatus, type ServerDetails, type ServerListItem, type ServerStatus } from '../../api'

export function getServerDisplayName(server: Pick<ServerListItem, 'name' | 'host' | 'queryPort'>) {
  return server.name ?? `${server.host}:${server.queryPort}`
}

export function formatAddress(server: Pick<ServerListItem, 'host' | 'queryPort'>) {
  return `${server.host}:${server.queryPort}`
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
