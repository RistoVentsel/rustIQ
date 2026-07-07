import { Link } from 'react-router-dom'

import type { ServerListItem } from '../../api'
import {
  formatAddress,
  formatRelativeTimestamp,
  formatServerLocationShort,
  getServerDisplayName,
} from './serverPresentation'

type ServerListCardProps = {
  server: ServerListItem
  nowMs: number
}

export function ServerListCard({ server, nowMs }: ServerListCardProps) {
  return (
    <Link to={`/servers/${server.id}`} className="server-list-card">
      <div className="server-list-card__identity">
        <h3 className="server-list-card__name">{getServerDisplayName(server)}</h3>
        <p className="server-list-card__address">
          Map: {server.map ?? 'Unavailable'} <span>PVE: False</span> <span>{formatAddress(server)}</span>
        </p>
      </div>

      <div className="server-list-card__cell">{server.players}/{server.maxPlayers}</div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.lastWipeAtUtc, nowMs)}</div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.nextWipeAtUtc, nowMs)}</div>
      <div className="server-list-card__cell">{formatServerLocationShort(server)}</div>
      <div className="server-list-card__cell">{formatRelativeTimestamp(server.updatedAtUtc, nowMs)}</div>
    </Link>
  )
}
