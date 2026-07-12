import {
  rustServerType,
  serverStatus,
  type RustServerType,
  type ServerDetails,
  type ServerListItem,
  type ServerStatus,
} from '../../api'
import { getLanguageLocale, type Language } from '../../i18n/language'

type ServerQueryEndpoint = Pick<ServerListItem | ServerDetails, 'host' | 'queryPort'>
type ServerConnectEndpoint = Pick<
  ServerListItem | ServerDetails,
  'address' | 'host' | 'gamePort' | 'queryPort'
>

const serverPresentationCopy: Record<Language, {
  unknown: string
  unavailable: string
  noTrend: string
  flatHour: string
  online: string
  offline: string
  official: string
  officialShort: string
  community: string
  communityShort: string
  modded: string
  moddedShort: string
}> = {
  en: {
    unknown: 'Unknown',
    unavailable: 'Unavailable',
    noTrend: 'No trend',
    flatHour: 'Flat 1h',
    online: 'Online',
    offline: 'Offline',
    official: 'Official',
    officialShort: 'Official',
    community: 'Community',
    communityShort: 'Community',
    modded: 'Modded',
    moddedShort: 'Modded',
  },
  ru: {
    unknown: 'Неизвестно',
    unavailable: 'Недоступно',
    noTrend: 'Нет тренда',
    flatHour: '0 1ч',
    online: 'Онлайн',
    offline: 'Офлайн',
    official: 'Официальный',
    officialShort: 'Офиц.',
    community: 'Комьюнити',
    communityShort: 'Комьюн.',
    modded: 'Моддед',
    moddedShort: 'Моддед',
  },
}

export function getServerDisplayName(server: Pick<ServerListItem, 'name' | 'host' | 'queryPort'>) {
  return server.name ?? `${server.host}:${server.queryPort}`
}

export function formatAddress(server: ServerQueryEndpoint) {
  return `${server.host}:${server.queryPort}`
}

export function formatConnectAddress(server: ServerConnectEndpoint) {
  if (server.gamePort !== null && server.gamePort !== undefined) {
    return `${server.host}:${server.gamePort}`
  }

  const address = server.address?.trim()
  if (address) {
    return address
  }

  return formatAddress(server)
}

export function formatClientConnectCommand(server: ServerConnectEndpoint) {
  return `client.connect ${formatConnectAddress(server)}`
}

export function formatCountry(country: string | null | undefined, language: Language = 'en') {
  return country?.trim().toUpperCase() || serverPresentationCopy[language].unknown
}

export function formatPlayerCount(
  server: Pick<ServerListItem | ServerDetails, 'players' | 'maxPlayers' | 'queuePlayers'>,
) {
  const queueSuffix = server.queuePlayers && server.queuePlayers > 0
    ? ` (${server.queuePlayers})`
    : ''

  return `${server.players}/${server.maxPlayers}${queueSuffix}`
}

export function formatPlayerTrend(
  server: Pick<ServerListItem, 'playersDeltaLastHour' | 'playersPeakLastHour'>,
  language: Language = 'en',
) {
  if (server.playersDeltaLastHour === null || server.playersDeltaLastHour === undefined) {
    return serverPresentationCopy[language].noTrend
  }

  if (server.playersDeltaLastHour === 0) {
    return serverPresentationCopy[language].flatHour
  }

  const prefix = server.playersDeltaLastHour > 0 ? '+' : ''
  const hourLabel = language === 'ru' ? '1ч' : '1h'
  return `${prefix}${server.playersDeltaLastHour} ${hourLabel}`
}

export function getPlayerTrendClassName(
  server: Pick<ServerListItem, 'playersDeltaLastHour'>,
) {
  if (server.playersDeltaLastHour === null || server.playersDeltaLastHour === undefined) {
    return 'server-trend server-trend--unknown'
  }

  if (server.playersDeltaLastHour > 0) {
    return 'server-trend server-trend--up'
  }

  if (server.playersDeltaLastHour < 0) {
    return 'server-trend server-trend--down'
  }

  return 'server-trend server-trend--flat'
}

export function formatServerLocation(
  server: Pick<ServerListItem | ServerDetails, 'country' | 'latitude' | 'longitude'>,
  language: Language = 'en',
) {
  const country = formatCountry(server.country, language)
  if (server.latitude === null || server.latitude === undefined
      || server.longitude === null || server.longitude === undefined) {
    return country
  }

  return `${country} (${server.latitude.toFixed(2)}, ${server.longitude.toFixed(2)})`
}

export function formatServerLocationShort(
  server: Pick<ServerListItem | ServerDetails, 'country' | 'latitude' | 'longitude'>,
  language: Language = 'en',
) {
  const flag = getCountryFlag(server.country)
  const country = formatCountry(server.country, language)
  if (server.latitude === null || server.latitude === undefined
      || server.longitude === null || server.longitude === undefined) {
    return `${flag}${country}`.trim()
  }

  return `${flag}${country} ${server.latitude.toFixed(1)}, ${server.longitude.toFixed(1)}`.trim()
}

export function formatServerTimestamp(timestamp: string | null | undefined, language: Language = 'en') {
  if (!timestamp) {
    return serverPresentationCopy[language].unavailable
  }

  const parsed = Date.parse(timestamp)
  if (Number.isNaN(parsed)) {
    return serverPresentationCopy[language].unavailable
  }

  return new Intl.DateTimeFormat(getLanguageLocale(language), {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parsed)
}

export function formatRelativeTimestamp(timestamp: string | null | undefined, nowMs = Date.now(), language: Language = 'en') {
  if (!timestamp) {
    return serverPresentationCopy[language].unknown
  }

  const parsed = Date.parse(timestamp)
  if (Number.isNaN(parsed)) {
    return serverPresentationCopy[language].unknown
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

  return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'auto' }).format(value, unit)
}

export function getServerLastActivityTimestamp(server: Pick<ServerListItem, 'lastCheckedAtUtc' | 'updatedAtUtc'>) {
  return server.lastCheckedAtUtc ?? server.updatedAtUtc
}

export function getServerStatusLabel(status: ServerStatus, language: Language = 'en') {
  switch (status) {
    case serverStatus.Online:
      return serverPresentationCopy[language].online
    case serverStatus.Offline:
      return serverPresentationCopy[language].offline
    default:
      return serverPresentationCopy[language].unknown
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

export function getRustServerTypeLabel(
  serverType: RustServerType | null | undefined,
  language: Language = 'en',
  variant: 'long' | 'short' = 'long',
) {
  const copy = serverPresentationCopy[language]

  switch (serverType) {
    case rustServerType.Official:
      return variant === 'short' ? copy.officialShort : copy.official
    case rustServerType.Community:
      return variant === 'short' ? copy.communityShort : copy.community
    case rustServerType.Modded:
      return variant === 'short' ? copy.moddedShort : copy.modded
    default:
      return copy.unknown
  }
}

export function getRustServerTypeClassName(serverType: RustServerType | null | undefined) {
  switch (serverType) {
    case rustServerType.Official:
      return 'server-type-badge server-type-badge--official'
    case rustServerType.Community:
      return 'server-type-badge server-type-badge--community'
    case rustServerType.Modded:
      return 'server-type-badge server-type-badge--modded'
    default:
      return 'server-type-badge server-type-badge--unknown'
  }
}

export function formatServerSubtitle(
  server: Pick<ServerListItem, 'host' | 'queryPort' | 'map'> | Pick<ServerDetails, 'host' | 'queryPort' | 'map'>,
) {
  const address = formatAddress(server)
  return server.map ? `${address} | ${server.map}` : address
}

export function getCountryFlag(country: string | null | undefined) {
  const code = country?.trim().toUpperCase()
  if (!code || code.length !== 2 || !/^[A-Z]{2}$/.test(code)) {
    return ''
  }

  return `${String.fromCodePoint(0x1f1e6 + code.charCodeAt(0) - 65)}${String.fromCodePoint(
    0x1f1e6 + code.charCodeAt(1) - 65,
  )} `
}
