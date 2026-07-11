export const serverStatus = {
  Unknown: 0,
  Online: 1,
  Offline: 2,
} as const

export type ServerStatus = (typeof serverStatus)[keyof typeof serverStatus]

export const rustServerType = {
  Unknown: 0,
  Official: 1,
  Community: 2,
  Modded: 3,
} as const

export type RustServerType = (typeof rustServerType)[keyof typeof rustServerType]

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
}

export type ServerRefreshStatus = {
  isQueuedOrRunning: boolean
  isRunning: boolean
  queuedAtUtc: string | null
  startedAtUtc: string | null
  completedAtUtc: string | null
  failedAtUtc: string | null
  lastErrorMessage: string | null
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
  durationSeconds: number
  seenAtUtc: string
}

export type CurrentPlayersResponse = {
  refreshedAtUtc: string | null
  players: CurrentPlayer[]
}

export type PatchNote = {
  title: string
  summary: string
  url: string
  imageUrl: string | null
  publishedAtUtc: string
}

export type ServerListItem = {
  id: string
  battleMetricsServerId: number | null
  address: string | null
  host: string
  gamePort: number | null
  queryPort: number
  country: string | null
  latitude: number | null
  longitude: number | null
  lastWipeAtUtc: string | null
  nextWipeAtUtc: string | null
  name: string | null
  map: string | null
  players: number
  maxPlayers: number
  queuePlayers: number | null
  serverType: RustServerType
  status: ServerStatus
  lastValidationReason: string | null
  lastSeenAtUtc: string | null
  lastCheckedAtUtc: string | null
  lastDiscoveredAtUtc: string | null
  lastPlayerCountUpdatedAtUtc: string | null
  updatedAtUtc: string
  updatedSecondsAgo: number
  playersDeltaLastHour: number | null
  playersPeakLastHour: number | null
  popularityRank: number | null
  rankedAveragePlayers: number | null
  rankedPeakPlayers: number | null
  rankingSnapshotCount: number | null
  rankingGeneratedAtUtc: string | null
}

export type ServerDetails = {
  id: string
  battleMetricsServerId: number | null
  address: string | null
  host: string
  gamePort: number | null
  queryPort: number
  country: string | null
  latitude: number | null
  longitude: number | null
  lastWipeAtUtc: string | null
  nextWipeAtUtc: string | null
  name: string | null
  map: string | null
  players: number
  maxPlayers: number
  queuePlayers: number | null
  serverType: RustServerType
  status: ServerStatus
  lastSeenAtUtc: string | null
  lastCheckedAtUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
  updatedSecondsAgo: number
  lastSeenSecondsAgo: number | null
  mapMetadata: ServerMapSummary | null
}
