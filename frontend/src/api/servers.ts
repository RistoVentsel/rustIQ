import { getJson, postJson } from './http'
import type {
  CurrentPlayersResponse,
  PagedResult,
  ServerDetails,
  ServerListItem,
  ServerMapMetadataResponse,
  ServerRefreshStatus,
  ServerSnapshot,
  ServerStatus,
} from './types'

export type GetServersParams = {
  search?: string
  status?: ServerStatus
  page?: number
  pageSize?: number
  sortBy?: 'players' | 'lastWipe' | 'nextWipe' | 'location'
  sortDirection?: 'asc' | 'desc'
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
    query: params,
  })
}

export function queueServerRefresh(signal?: AbortSignal) {
  return postJson<ServerRefreshStatus>('servers/refresh', { signal })
}

export function getServerRefreshStatus(signal?: AbortSignal) {
  return getJson<ServerRefreshStatus>('servers/refresh', { signal })
}

export function getServerById(serverId: string, signal?: AbortSignal, refresh?: 'page') {
  return getJson<ServerDetails>(`servers/${serverId}`, {
    signal,
    query: refresh ? { refresh } : undefined,
  })
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
