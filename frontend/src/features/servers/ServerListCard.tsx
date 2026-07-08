import { Link } from 'react-router-dom'

import type { ServerListItem } from '../../api'
import {
  formatAddress,
  formatRelativeTimestamp,
  formatCountry,
  formatPlayerCount,
  getRustServerTypeClassName,
  getRustServerTypeLabel,
  getServerDisplayName,
} from './serverPresentation'

type ServerListCardProps = {
  server: ServerListItem
  nowMs: number
}

export function ServerListCard({ server, nowMs }: ServerListCardProps) {
  const country = formatCountry(server.country)
  const serverTypeLabel = getRustServerTypeLabel(server.serverType)
  const countryCode = server.country?.trim().toLowerCase()
  const flagSrc = countryCode && /^[a-z]{2}$/.test(countryCode)
    ? `https://flagcdn.com/w40/${countryCode}.png`
    : null

  return (
    <Link to={`/servers/${server.id}`} className="server-list-card">
      <div className="server-list-card__identity">
        <h3 className="server-list-card__name">{getServerDisplayName(server)}</h3>
        <p className="server-list-card__address">
          Map: {server.map ?? 'Unavailable'} <span>Type: {serverTypeLabel}</span> <span>PVE: False</span> <span>{formatAddress(server)}</span>
        </p>
      </div>

      <div className="server-list-card__cell">
        <span className={getRustServerTypeClassName(server.serverType)}>{serverTypeLabel}</span>
      </div>
      <div className="server-list-card__cell">{formatPlayerCount(server)}</div>
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
    </Link>
  )
}
