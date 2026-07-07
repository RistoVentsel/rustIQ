import { Link } from 'react-router-dom'

import type { ServerListItem } from '../../api'
import {
  formatAddress,
  formatServerLocation,
  formatServerTimestamp,
  getServerDisplayName,
  getServerStatusClassName,
  getServerStatusLabel,
} from './serverPresentation'

type ServerListCardProps = {
  server: ServerListItem
}

export function ServerListCard({ server }: ServerListCardProps) {
  return (
    <Link to={`/servers/${server.id}`} className="server-list-card">
      <div className="server-list-card__identity">
        <h3 className="server-list-card__name">{getServerDisplayName(server)}</h3>
        <p className="server-list-card__address">{formatAddress(server)}</p>
      </div>

      <dl className="server-list-card__stats">
        <div className="server-list-card__stat">
          <dt>Players</dt>
          <dd>
            {server.players} / {server.maxPlayers}
          </dd>
        </div>
        <div className="server-list-card__stat">
          <dt>Map</dt>
          <dd>{server.map ?? 'Unavailable'}</dd>
        </div>
        <div className="server-list-card__stat">
          <dt>Location</dt>
          <dd>{formatServerLocation(server)}</dd>
        </div>
        <div className="server-list-card__stat">
          <dt>Last wiped</dt>
          <dd>{formatServerTimestamp(server.lastWipeAtUtc)}</dd>
        </div>
        <div className="server-list-card__stat">
          <dt>Next wipe</dt>
          <dd>{formatServerTimestamp(server.nextWipeAtUtc)}</dd>
        </div>
      </dl>

      <span className={getServerStatusClassName(server.status)}>
        {getServerStatusLabel(server.status)}
      </span>
    </Link>
  )
}
