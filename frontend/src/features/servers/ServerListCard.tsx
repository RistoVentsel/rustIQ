import { Link } from 'react-router-dom'

import type { ServerListItem } from '../../api'
import {
  formatAddress,
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
      <div className="server-list-card__top">
        <div className="server-list-card__identity">
          <h3 className="server-list-card__name">{getServerDisplayName(server)}</h3>
          <p className="server-list-card__address">{formatAddress(server)}</p>
        </div>
        <span className={getServerStatusClassName(server.status)}>
          {getServerStatusLabel(server.status)}
        </span>
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
          <dt>Updated</dt>
          <dd>{server.updatedSecondsAgo}s ago</dd>
        </div>
      </dl>
    </Link>
  )
}
