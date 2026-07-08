import { Link } from 'react-router-dom'

import type { ServerListItem } from '../../api'
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

type ServerListCardProps = {
  server: ServerListItem
  nowMs: number
  isFavorite: boolean
  onToggleFavorite: (serverId: string) => void
}

export function ServerListCard({
  server,
  nowMs,
  isFavorite,
  onToggleFavorite,
}: ServerListCardProps) {
  const country = formatCountry(server.country)
  const serverTypeLabel = getRustServerTypeLabel(server.serverType)
  const displayName = getServerDisplayName(server)
  const countryCode = server.country?.trim().toLowerCase()
  const flagSrc = countryCode && /^[a-z]{2}$/.test(countryCode)
    ? `https://flagcdn.com/w40/${countryCode}.png`
    : null

  return (
    <article className="server-list-card">
      <button
        type="button"
        className={isFavorite ? 'server-favorite server-favorite--active' : 'server-favorite'}
        onClick={() => onToggleFavorite(server.id)}
        aria-label={isFavorite ? `Remove ${displayName} from favorites` : `Add ${displayName} to favorites`}
        aria-pressed={isFavorite}
      >
        {isFavorite ? 'Favorited' : 'Favorite'}
      </button>

      <Link to={`/servers/${server.id}`} className="server-list-card__identity">
        <h3 className="server-list-card__name">{displayName}</h3>
        <p className="server-list-card__address">
          Map: {server.map ?? 'Unavailable'} <span>Type: {serverTypeLabel}</span> <span>{formatAddress(server)}</span>
        </p>
      </Link>

      <div className="server-list-card__cell">
        <span className={getRustServerTypeClassName(server.serverType)}>{serverTypeLabel}</span>
      </div>
      <div className="server-list-card__cell">{formatPlayerCount(server)}</div>
      <div className="server-list-card__cell">
        <span
          className={getPlayerTrendClassName(server)}
          title={server.playersPeakLastHour === null || server.playersPeakLastHour === undefined
            ? undefined
            : `Peak last hour: ${server.playersPeakLastHour}`}
        >
          {formatPlayerTrend(server)}
        </span>
      </div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.lastWipeAtUtc, nowMs)}</div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.nextWipeAtUtc, nowMs)}</div>
      <div className="server-list-card__cell server-list-card__cell--location">
        <span className="server-location" aria-label={`Location ${country}`}>
          <span className="server-location__initials">{country}</span>
          {flagSrc ? (
            <img className="server-location__flag" src={flagSrc} alt="" loading="lazy" />
          ) : (
            <span className="server-location__flag server-location__flag--empty" aria-hidden="true" />
          )}
          <span className="server-location__pulse" aria-hidden="true" />
        </span>
      </div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.updatedAtUtc, nowMs)}</div>
    </article>
  )
}
