import { Link } from 'react-router-dom'

import { serverStatus, type ServerListItem } from '../../api'
import type { Language } from '../../i18n/language'
import {
  formatAddress,
  formatRelativeTimestamp,
  formatCountry,
  formatPlayerCount,
  formatPlayerTrend,
  getPlayerTrendClassName,
  getRustServerTypeClassName,
  getRustServerTypeLabel,
  getServerDisplayName,
} from './serverPresentation'

const serverListCardCopy: Record<Language, {
  addFavorite: (serverName: string) => string
  removeFavorite: (serverName: string) => string
  averageTitle: (average: string, peak: number | string) => string
  map: string
  unavailable: string
  peakLastHour: (peak: number) => string
  location: (country: string) => string
}> = {
  en: {
    addFavorite: (serverName) => `Add ${serverName} to favorites`,
    removeFavorite: (serverName) => `Remove ${serverName} from favorites`,
    averageTitle: (average, peak) => `Average ${average}, peak ${peak}`,
    map: 'Map',
    unavailable: 'Unavailable',
    peakLastHour: (peak) => `Peak last hour: ${peak}`,
    location: (country) => `Location ${country}`,
  },
  ru: {
    addFavorite: (serverName) => `Добавить ${serverName} в избранное`,
    removeFavorite: (serverName) => `Убрать ${serverName} из избранного`,
    averageTitle: (average, peak) => `Среднее ${average}, пик ${peak}`,
    map: 'Карта',
    unavailable: 'Недоступно',
    peakLastHour: (peak) => `Пик за последний час: ${peak}`,
    location: (country) => `Локация ${country}`,
  },
}

type ServerListCardProps = {
  server: ServerListItem
  nowMs: number
  isFavorite: boolean
  language: Language
  onToggleFavorite: (serverId: string) => void
}

export function ServerListCard({
  server,
  nowMs,
  isFavorite,
  language,
  onToggleFavorite,
}: ServerListCardProps) {
  const copy = serverListCardCopy[language]
  const isOffline = server.status === serverStatus.Offline
  const country = formatCountry(server.country, language)
  const serverTypeLabel = getRustServerTypeLabel(server.serverType, language, 'short')
  const serverTypeFullLabel = getRustServerTypeLabel(server.serverType, language)
  const displayName = getServerDisplayName(server)
  const countryCode = server.country?.trim().toLowerCase()
  const flagSrc = countryCode && /^[a-z]{2}$/.test(countryCode)
    ? `https://flagcdn.com/w40/${countryCode}.png`
    : null
  // The pill is a fixed-width cell, so the badge carries the short code only and
  // the spelled-out country stays in the label and tooltip.
  const countryBadge = server.country?.trim().toUpperCase() || '--'
  // A missing trend is spelled out in the tooltip rather than in the pill, which
  // otherwise has to be sized for "No trend" on every row that has a trend.
  const trendLabel = formatPlayerTrend(server, language)
  const hasTrend = server.playersDeltaLastHour !== null && server.playersDeltaLastHour !== undefined
  const peak = server.playersPeakLastHour
  const trendTitle = peak === null || peak === undefined
    ? (hasTrend ? undefined : trendLabel)
    : copy.peakLastHour(peak)

  return (
    <article className={isOffline ? 'server-list-card server-list-card--offline' : 'server-list-card'}>
      <button
        type="button"
        className={isFavorite ? 'server-favorite server-favorite--active' : 'server-favorite'}
        onClick={() => onToggleFavorite(server.id)}
        aria-label={isFavorite ? copy.removeFavorite(displayName) : copy.addFavorite(displayName)}
        aria-pressed={isFavorite}
      >
        <span aria-hidden="true">{isFavorite ? '★' : '☆'}</span>
      </button>

      <div className="server-list-card__cell">
        {server.popularityRank ? (
          <span
            className="server-rank-badge"
            title={
              server.rankedAveragePlayers === null || server.rankedAveragePlayers === undefined
                ? undefined
                : copy.averageTitle(server.rankedAveragePlayers.toFixed(1), server.rankedPeakPlayers ?? 'n/a')
            }
          >
            #{server.popularityRank}
          </span>
        ) : (
          <span className="server-rank-badge server-rank-badge--empty">--</span>
        )}
      </div>

      <Link to={`/servers/${server.id}`} className="server-list-card__identity">
        <h3 className="server-list-card__name">{displayName}</h3>
        <p className="server-list-card__address">
          {copy.map}: {server.map ?? copy.unavailable} <span>{formatAddress(server)}</span>
        </p>
      </Link>

      <div className="server-list-card__cell">
        <span
          className={getRustServerTypeClassName(server.serverType)}
          title={serverTypeFullLabel}
          aria-label={serverTypeFullLabel}
        >
          {serverTypeLabel}
        </span>
      </div>
      <div
        className={
          isOffline
            ? 'server-list-card__cell server-list-card__cell--players server-list-card__cell--offline'
            : 'server-list-card__cell server-list-card__cell--players'
        }
      >
        {formatPlayerCount(server)}
      </div>
      <div className="server-list-card__cell server-list-card__cell--trend">
        <span
          className={getPlayerTrendClassName(server)}
          aria-label={hasTrend ? undefined : trendLabel}
          title={trendTitle}
        >
          {hasTrend ? trendLabel : '--'}
        </span>
      </div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.lastWipeAtUtc, nowMs, language)}</div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.nextWipeAtUtc, nowMs, language)}</div>
      <div className="server-list-card__cell server-list-card__cell--location">
        <span className="server-location" aria-label={copy.location(country)} title={country}>
          <span className="server-location__initials">{countryBadge}</span>
          {flagSrc ? (
            <img className="server-location__flag" src={flagSrc} alt="" loading="lazy" />
          ) : (
            <span className="server-location__flag server-location__flag--empty" aria-hidden="true" />
          )}
          <span className="server-location__pulse" aria-hidden="true" />
        </span>
      </div>
    </article>
  )
}
