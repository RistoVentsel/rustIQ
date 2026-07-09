import { getJson, postJson } from './http'
import type {
  CurrentPlayersResponse,
  PagedResult,
  RustServerType,
  ServerDetails,
  ServerListItem,
  ServerMapMetadataResponse,
  ServerRefreshStatus,
  ServerSnapshot,
  ServerStatus,
} from './types'
import { rustServerType } from './types'

export type GetServersParams = {
  search?: string
  status?: ServerStatus
  serverTypes?: RustServerType[]
  page?: number
  pageSize?: number
  sortBy?: 'players' | 'lastWipe' | 'nextWipe' | 'location' | 'serverType'
  sortDirection?: 'asc' | 'desc'
  wipeFilter?: 'next24h' | 'next7d' | 'wiped24h' | 'hasWipeData'
  refresh?: 'page' | 'all'
}

export type GetServerSnapshotsParams = {
  from?: string
  to?: string
  limit?: number
}

export function getServers(signal?: AbortSignal, params: GetServersParams = {}) {
  return getJson<PagedResult<ServerListItem>>('servers', {
    signal,
    query: {
      ...params,
      serverTypes: formatServerTypes(params.serverTypes),
    },
  })
}

function formatServerTypes(serverTypes: RustServerType[] | undefined) {
  if (!serverTypes || serverTypes.length === 0) {
    return undefined
  }

  return serverTypes
    .flatMap((serverType) => {
      switch (serverType) {
        case rustServerType.Official:
          return ['official']
        case rustServerType.Community:
          return ['community']
        case rustServerType.Modded:
          return ['modded']
        default:
          return []
      }
    })
    .join(',')
}

export function queueServerRefresh(signal?: AbortSignal) {
  return postJson<ServerRefreshStatus>('servers/refresh', { signal })
}

export function getServerRefreshStatus(signal?: AbortSignal) {
  return getJson<ServerRefreshStatus>('servers/refresh', { signal })
}

export function getServerById(serverId: string, signal?: AbortSignal) {
  return getJson<ServerDetails>(`servers/${serverId}`, { signal })
}

export function getServerSnapshots(
  serverId: string,
  signal?: AbortSignal,
  params: GetServerSnapshotsParams = {},
) {
  return getJson<ServerSnapshot[]>(`servers/${serverId}/snapshots`, {
    signal,
    query: params,
  })
}

export function getServerPlayers(serverId: string, signal?: AbortSignal) {
  return getJson<CurrentPlayersResponse>(`servers/${serverId}/players`, { signal })
}

export function getServerMap(serverId: string, signal?: AbortSignal) {
  return getJson<ServerMapMetadataResponse>(`servers/${serverId}/map`, { signal })
}
