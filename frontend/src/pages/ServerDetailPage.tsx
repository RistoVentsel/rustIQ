import { useQuery } from '@tanstack/react-query'
import { useRef } from 'react'
import { Link, useParams } from 'react-router-dom'

import { ApiError, getServerById, getServerMap, getServerPlayers, getServerSnapshots } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { ServerLayoutPreview } from '../features/servers/ServerLayoutPreview'
import {
  formatServerLocation,
  formatServerSubtitle,
  formatServerTimestamp,
  getServerDisplayName,
  getServerStatusLabel,
} from '../features/servers/serverPresentation'
import { useServerLiveUpdates } from '../features/servers/useServerLiveUpdates'
import { useNow } from '../hooks'
import { formatSecondsAgo, getSecondsAgo } from '../lib/time'

function getLiveStatusLabel(status: ReturnType<typeof useServerLiveUpdates>) {
  switch (status) {
    case 'connected':
      return 'Live connected'
    case 'reconnecting':
      return 'Reconnecting'
    case 'connecting':
      return 'Connecting'
    case 'error':
      return 'Live error'
    case 'disconnected':
      return 'Disconnected'
    default:
      return 'Live idle'
  }
}

function getLiveStatusClassName(status: ReturnType<typeof useServerLiveUpdates>) {
  switch (status) {
    case 'connected':
      return 'status-pill status-pill--online'
    case 'reconnecting':
    case 'connecting':
      return 'status-pill status-pill--unknown'
    case 'error':
    case 'disconnected':
      return 'status-pill status-pill--offline'
    default:
      return 'status-pill status-pill--unknown'
  }
}

function getQueryErrorDetail(error: unknown) {
  return error instanceof ApiError
    ? `${error.message} (${error.url})`
    : 'Unexpected error while loading backend data.'
}

export function ServerDetailPage() {
  const { serverId } = useParams<{ serverId: string }>()
  const liveStatus = useServerLiveUpdates(serverId)
  const nowMs = useNow()
  const refreshOnInitialLoadRef = useRef<'page' | null>('page')
  const serverQuery = useQuery({
    queryKey: ['server', serverId],
    enabled: Boolean(serverId),
    queryFn: ({ signal }) => {
      const refresh = refreshOnInitialLoadRef.current
      refreshOnInitialLoadRef.current = null
      return getServerById(serverId!, signal, refresh ?? undefined)
    },
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const playersQuery = useQuery({
    queryKey: ['server', serverId, 'players'],
    enabled: Boolean(serverId),
    queryFn: ({ signal }) => getServerPlayers(serverId!, signal),
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const mapQuery = useQuery({
    queryKey: ['server', serverId, 'map'],
    enabled: Boolean(serverId),
    queryFn: ({ signal }) => getServerMap(serverId!, signal),
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const snapshotsQuery = useQuery({
    queryKey: ['server', serverId, 'snapshots', { limit: 20 }],
    enabled: Boolean(serverId),
    queryFn: ({ signal }) => getServerSnapshots(serverId!, signal, { limit: 20 }),
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const players = playersQuery.data?.players ?? []
  const snapshots = snapshotsQuery.data ?? []
  const hasPlayerSnapshot = Boolean(playersQuery.data?.refreshedAtUtc)
  const hasServerData = Boolean(serverQuery.data)
  const updatedSecondsAgo = serverQuery.data
    ? getSecondsAgo(serverQuery.data.updatedAtUtc, nowMs)
    : null
  const lastSeenSecondsAgo = serverQuery.data?.lastSeenAtUtc
    ? getSecondsAgo(serverQuery.data.lastSeenAtUtc, nowMs)
    : null

  return (
    <div className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">Server Detail</p>
        <h2 className="page-heading">Server detail route</h2>
        <p className="page-text">
          This screen loads one specific server from the backend, along with current players
          and map metadata. It also subscribes to live updates and shows recent snapshot
          history from cached data.
        </p>
      </div>

      <SectionCard
        title="Server overview"
        description="Route-specific backend data for the selected server."
        actions={
          <div className="server-detail-actions">
            <span className={getLiveStatusClassName(liveStatus)}>{getLiveStatusLabel(liveStatus)}</span>
            <Link to="/" className="button-link button-link--secondary">
              Back
            </Link>
          </div>
        }
      >
        {!serverId ? (
          <StateNotice title="Server id is missing from the route" tone="error" />
        ) : null}

        {liveStatus === 'connecting' ? (
          <StateNotice
            title="Connecting to live updates"
            description="The page is opening its SignalR subscription for this server."
            tone="info"
          />
        ) : null}

        {liveStatus === 'reconnecting' ? (
          <StateNotice
            title="Reconnecting live updates"
            description="Live pushes paused temporarily. Cached server data is still visible while SignalR reconnects."
            tone="warning"
          />
        ) : null}

        {liveStatus === 'disconnected' || liveStatus === 'error' ? (
          <StateNotice
            title="Live updates unavailable"
            description="The page could not keep the SignalR subscription active. Cached REST data is still shown below."
            tone="error"
          />
        ) : null}

        {serverQuery.isLoading && !hasServerData ? (
          <StateNotice
            title="Loading server details"
            description="The detail page is waiting for GET /servers/{id}."
          />
        ) : null}

        {serverQuery.isFetching && hasServerData ? (
          <StateNotice
            title="Refreshing server details"
            description="Showing the current server snapshot while the detail query refreshes in the background."
          />
        ) : null}

        {serverQuery.isError ? (
          <StateNotice
            title={hasServerData ? 'Server refresh failed, showing cached details' : 'Server detail request failed'}
            description={
              hasServerData
                ? 'The latest detail refresh failed, so the page is still using the last successful server snapshot.'
                : undefined
            }
            detail={getQueryErrorDetail(serverQuery.error)}
            tone="error"
          />
        ) : null}

        {serverQuery.data ? (
          <div className="page-stack">
            <ServerLayoutPreview
              name={getServerDisplayName(serverQuery.data)}
              subtitle={formatServerSubtitle(serverQuery.data)}
              badgeLabel={getServerStatusLabel(serverQuery.data.status)}
              stats={[
                { label: 'Players', value: `${serverQuery.data.players} / ${serverQuery.data.maxPlayers}` },
                { label: 'Updated', value: formatSecondsAgo(updatedSecondsAgo) },
                {
                  label: 'Last seen',
                  value: formatSecondsAgo(lastSeenSecondsAgo),
                },
                { label: 'Last wiped', value: formatServerTimestamp(serverQuery.data.lastWipeAtUtc) },
                { label: 'Next wipe', value: formatServerTimestamp(serverQuery.data.nextWipeAtUtc) },
                { label: 'Location', value: formatServerLocation(serverQuery.data) },
              ]}
            />

            <dl className="server-detail-meta">
              <div className="server-detail-meta__item">
                <dt>Server id</dt>
                <dd>{serverQuery.data.id}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Address</dt>
                <dd>
                  {serverQuery.data.host}:{serverQuery.data.queryPort}
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Created</dt>
                <dd>{serverQuery.data.createdAtUtc}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Updated</dt>
                <dd>{formatSecondsAgo(updatedSecondsAgo)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Last checked</dt>
                <dd>{serverQuery.data.lastCheckedAtUtc ?? 'Unavailable'}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Last wiped</dt>
                <dd>{formatServerTimestamp(serverQuery.data.lastWipeAtUtc)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Next wipe</dt>
                <dd>{formatServerTimestamp(serverQuery.data.nextWipeAtUtc)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Location</dt>
                <dd>{formatServerLocation(serverQuery.data)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Map summary</dt>
                <dd>{serverQuery.data.mapMetadata?.mapName ?? serverQuery.data.map ?? 'Unavailable'}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Live connection</dt>
                <dd>{getLiveStatusLabel(liveStatus)}</dd>
              </div>
            </dl>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard
        title="Map"
        description="Map metadata from GET /servers/{id}/map."
      >
        {mapQuery.isLoading && mapQuery.data === undefined ? (
          <StateNotice
            title="Loading map metadata"
            description="The page is waiting for GET /servers/{id}/map."
          />
        ) : null}

        {mapQuery.isFetching && mapQuery.data !== undefined ? (
          <StateNotice
            title="Refreshing map metadata"
            description="Showing the current map section while the backend metadata refreshes."
          />
        ) : null}

        {mapQuery.isError ? (
          <StateNotice
            title={mapQuery.data !== undefined ? 'Map refresh failed, showing cached metadata' : 'Map metadata request failed'}
            description={
              mapQuery.data !== undefined
                ? 'The newest map metadata could not be loaded, so the section is still showing the last successful response.'
                : undefined
            }
            detail={getQueryErrorDetail(mapQuery.error)}
            tone="error"
          />
        ) : null}

        {!mapQuery.isLoading && !mapQuery.isError && mapQuery.data === null ? (
          <StateNotice
            title="Map metadata unavailable"
            description="The server exists, but the backend has no stored map metadata yet."
          />
        ) : null}

        {mapQuery.data ? (
          <div className="page-stack">
            {!mapQuery.data.thumbnailUrl ? (
              <StateNotice
                title="Map thumbnail unavailable"
                description="Showing text-only map metadata because no thumbnail image has been stored for this server."
              />
            ) : null}

            <div className="server-map-layout">
            {mapQuery.data.thumbnailUrl ? (
              <a
                href={mapQuery.data.rustMapsUrl ?? mapQuery.data.thumbnailUrl}
                target="_blank"
                rel="noreferrer"
                className="server-map-thumb"
              >
                <img
                  src={mapQuery.data.thumbnailUrl}
                  alt={mapQuery.data.mapName ?? 'Server map thumbnail'}
                  className="server-map-thumb__image"
                />
              </a>
            ) : null}

            <dl className="server-detail-meta">
              <div className="server-detail-meta__item">
                <dt>Map name</dt>
                <dd>{mapQuery.data.mapName ?? 'Unavailable'}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Seed</dt>
                <dd>{mapQuery.data.seed ?? 'Unavailable'}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Size</dt>
                <dd>{mapQuery.data.size ?? 'Unavailable'}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Source</dt>
                <dd>{mapQuery.data.source}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>RustMaps</dt>
                <dd>
                  {mapQuery.data.rustMapsUrl ? (
                    <a
                      href={mapQuery.data.rustMapsUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-link"
                    >
                      Open RustMaps
                    </a>
                  ) : (
                    'Unavailable'
                  )}
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>Map file</dt>
                <dd>
                  {mapQuery.data.mapFileUrl ? (
                    <a
                      href={mapQuery.data.mapFileUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-link"
                    >
                      Open map file
                    </a>
                  ) : (
                    'Unavailable'
                  )}
                </dd>
              </div>
            </dl>
            </div>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard
        title="Current players"
        description="Player data from GET /servers/{id}/players."
      >
        {playersQuery.isLoading && playersQuery.data === undefined ? (
          <StateNotice
            title="Loading current players"
            description="The page is waiting for GET /servers/{id}/players."
          />
        ) : null}

        {playersQuery.isFetching && playersQuery.data !== undefined ? (
          <StateNotice
            title="Refreshing current players"
            description="Showing the latest stored player list while the backend refreshes this section."
          />
        ) : null}

        {playersQuery.isError ? (
          <StateNotice
            title={
              playersQuery.data !== undefined
                ? 'Player list refresh failed, showing cached players'
                : 'Current players request failed'
            }
            description={
              playersQuery.data !== undefined
                ? 'The newest player list could not be loaded, so this section is still using the last successful response.'
                : undefined
            }
            detail={getQueryErrorDetail(playersQuery.error)}
            tone="error"
          />
        ) : null}

        {!playersQuery.isLoading && !playersQuery.isError && players.length === 0 ? (
          <StateNotice
            title={hasPlayerSnapshot ? 'No players currently listed' : 'Current player list unavailable'}
            description={
              hasPlayerSnapshot
                ? 'The backend returned an empty player list for this server.'
                : 'The backend has not stored a current player list for this server yet.'
            }
          />
        ) : null}

        {!playersQuery.isLoading && !playersQuery.isError && players.length > 0 ? (
          <div className="page-stack">
            <p className="page-text">
              Refreshed at {playersQuery.data?.refreshedAtUtc ?? 'Unavailable'}
            </p>

            <div className="player-list">
              {players.map((player, index) => (
                <div key={`${player.playerName ?? 'player'}-${index}`} className="player-list__item">
                  <div className="player-list__identity">
                    <strong>{player.playerName ?? 'Unnamed player'}</strong>
                    <span>Seen at {player.seenAtUtc}</span>
                  </div>
                  <dl className="player-list__stats">
                    <div>
                      <dt>Score</dt>
                      <dd>{player.score}</dd>
                    </div>
                    <div>
                      <dt>Duration</dt>
                      <dd>{Math.round(player.durationSeconds)}s</dd>
                    </div>
                  </dl>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard
        title="Snapshot history"
        description="Recent cached player-count history from GET /servers/{id}/snapshots."
      >
        {snapshotsQuery.isLoading && snapshotsQuery.data === undefined ? (
          <StateNotice
            title="Loading snapshot history"
            description="The page is waiting for GET /servers/{id}/snapshots."
          />
        ) : null}

        {snapshotsQuery.isFetching && snapshotsQuery.data !== undefined ? (
          <StateNotice
            title="Refreshing snapshot history"
            description="Showing the current history table while the backend refreshes recent snapshots."
          />
        ) : null}

        {snapshotsQuery.isError ? (
          <StateNotice
            title={
              snapshotsQuery.data !== undefined
                ? 'Snapshot refresh failed, showing cached history'
                : 'Snapshot history request failed'
            }
            description={
              snapshotsQuery.data !== undefined
                ? 'The newest snapshot history could not be loaded, so the table is still showing the last successful response.'
                : undefined
            }
            detail={getQueryErrorDetail(snapshotsQuery.error)}
            tone="error"
          />
        ) : null}

        {!snapshotsQuery.isLoading && !snapshotsQuery.isError && snapshots.length === 0 ? (
          <StateNotice
            title="No snapshot history available"
            description="The backend has not stored recent player-count snapshots for this server yet."
          />
        ) : null}

        {!snapshotsQuery.isLoading && !snapshotsQuery.isError && snapshots.length > 0 ? (
          <div className="snapshot-table-wrap">
            <table className="snapshot-table">
              <thead>
                <tr>
                  <th scope="col">Checked</th>
                  <th scope="col">Players</th>
                  <th scope="col">Status</th>
                  <th scope="col">Ping</th>
                </tr>
              </thead>
              <tbody>
                {snapshots.map((snapshot, index) => (
                  <tr key={`${snapshot.checkedAtUtc}-${index}`}>
                    <td>{snapshot.checkedAtUtc}</td>
                    <td>
                      {snapshot.players} / {snapshot.maxPlayers}
                    </td>
                    <td>{snapshot.isOnline ? 'Online' : 'Offline'}</td>
                    <td>
                      {snapshot.pingMilliseconds === null
                        ? 'Unavailable'
                        : `${snapshot.pingMilliseconds} ms`}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}
