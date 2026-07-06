export const serverStatus = {
  Unknown: 0,
  Online: 1,
  Offline: 2,
} as const

export type ServerStatus = (typeof serverStatus)[keyof typeof serverStatus]

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
}

export type ServerMapSummary = {
  mapName: string | null
  seed: number | null
  size: number | null
  rustMapsUrl: string | null
  thumbnailUrl: string | null
}

export type ServerMapMetadata = {
  mapName: string | null
  seed: number | null
  size: number | null
  rustMapsUrl: string | null
  thumbnailUrl: string | null
  mapFileUrl: string | null
  source: string
  sourceUpdatedAtUtc: string | null
  fetchedAtUtc: string
}

export type ServerMapMetadataResponse = ServerMapMetadata | null

export type ServerSnapshot = {
  checkedAtUtc: string
  players: number
  maxPlayers: number
  isOnline: boolean
  pingMilliseconds: number | null
}

export type CurrentPlayer = {
  playerName: string | null
  score: number
  durationSeconds: number
  seenAtUtc: string
}

export type CurrentPlayersResponse = {
  refreshedAtUtc: string | null
  players: CurrentPlayer[]
}

export type ServerListItem = {
  id: string
  host: string
  queryPort: number
  name: string | null
  map: string | null
  players: number
  maxPlayers: number
  status: ServerStatus
  lastSeenAtUtc: string | null
  lastCheckedAtUtc: string | null
  updatedAtUtc: string
  updatedSecondsAgo: number
}

export type ServerDetails = {
  id: string
  host: string
  queryPort: number
  name: string | null
  map: string | null
  players: number
  maxPlayers: number
  status: ServerStatus
  lastSeenAtUtc: string | null
  lastCheckedAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
  updatedSecondsAgo: number
  lastSeenSecondsAgo: number | null
  mapMetadata: ServerMapSummary | null
}
