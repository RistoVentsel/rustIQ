import { useQuery } from '@tanstack/react-query'
import { useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { ApiError, getServers, serverStatus } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { ServerListCard } from '../features/servers/ServerListCard'
import { useDebouncedValue } from '../hooks'

const pageSizeOptions = [10, 25, 50]
type ServerSortBy = 'players' | 'lastWipe' | 'nextWipe'
type ServerSortDirection = 'asc' | 'desc'

export function ServersPage() {
  const [searchInput, setSearchInput] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | 'online' | 'offline'>('all')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [sortBy, setSortBy] = useState<ServerSortBy>('players')
  const [sortDirection, setSortDirection] = useState<ServerSortDirection>('desc')
  const refreshOnNextRequestRef = useRef<'all' | null>('all')
  const debouncedSearch = useDebouncedValue(searchInput.trim(), 300)
  const statusValue = useMemo(() => {
    switch (statusFilter) {
      case 'online':
        return serverStatus.Online
      case 'offline':
        return serverStatus.Offline
      default:
        return undefined
    }
  }, [statusFilter])

  const serversQuery = useQuery({
    queryKey: ['servers', { search: debouncedSearch, status: statusValue, page, pageSize, sortBy, sortDirection }],
    queryFn: ({ signal }) => {
      const refresh = refreshOnNextRequestRef.current
      refreshOnNextRequestRef.current = null

      return getServers(signal, {
        search: debouncedSearch || undefined,
        status: statusValue,
        page,
        pageSize,
        sortBy,
        sortDirection,
        refresh: refresh ?? undefined,
      })
    },
    staleTime: 0,
    refetchOnMount: 'always',
  })
  const serversPageData = serversQuery.data ?? null
  const servers = serversPageData?.items ?? []
  const hasSingleServer = servers.length === 1
  const totalItems = serversPageData?.totalItems ?? 0
  const totalPages = serversPageData ? Math.max(1, Math.ceil(totalItems / serversPageData.pageSize)) : 1
  const hasServersData = Boolean(serversPageData)
  const isLoadingServers = serversQuery.isFetching && !hasServersData
  const isRefreshingServers = serversQuery.isFetching && hasServersData
  const shouldShowServerData = hasServersData && !serversQuery.isFetching
  const serverListErrorDetail =
    serversQuery.error instanceof ApiError
      ? `${serversQuery.error.message} (${serversQuery.error.url})`
      : 'Unexpected error while loading the server list.'

  function updateSearch(value: string) {
    setSearchInput(value)
    setPage(1)
  }

  function updateStatus(value: 'all' | 'online' | 'offline') {
    setStatusFilter(value)
    setPage(1)
  }

  function updatePageSize(value: number) {
    setPageSize(value)
    setPage(1)
  }

  function updateSort(nextSortBy: ServerSortBy) {
    const defaultDirection = nextSortBy === 'nextWipe' ? 'asc' : 'desc'
    setSortDirection((currentDirection) => (
      sortBy === nextSortBy
        ? currentDirection === 'desc' ? 'asc' : 'desc'
        : defaultDirection
    ))
    setSortBy(nextSortBy)

    setPage(1)
  }

  function getSortIndicator(column: ServerSortBy) {
    if (sortBy !== column) {
      return null
    }

    return <span aria-hidden="true">{sortDirection === 'desc' ? '↓' : '↑'}</span>
  }

  return (
    <div className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">Server List</p>
        <h2 className="page-heading">Cached Rust server snapshot</h2>
        <p className="page-text">
          This page loads stored backend data from <code>GET /servers</code>. Open a server to
          move into the detail route, where live behavior will attach in later phases.
        </p>
      </div>

      <SectionCard
        title="Servers"
        description="This page is cached and readable by design. It does not open a live SignalR connection."
        actions={
          <div className="server-list-actions">
            <label className="server-list-control">
              <span>Page size</span>
              <select
                value={pageSize}
                onChange={(event) => updatePageSize(Number(event.target.value))}
                className="server-list-select"
              >
                {pageSizeOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              className="button-link button-link--secondary"
              onClick={() => {
                refreshOnNextRequestRef.current = 'all'
                void serversQuery.refetch()
              }}
              disabled={serversQuery.isFetching}
            >
              {serversQuery.isFetching ? 'Refreshing...' : 'Refresh'}
            </button>
          </div>
        }
      >
        <div className="server-list-filters">
          <label className="server-list-control server-list-control--search">
            <span>Search</span>
            <input
              type="search"
              value={searchInput}
              onChange={(event) => updateSearch(event.target.value)}
              placeholder="Search by name, host, or map"
              className="server-list-input"
            />
          </label>

          <label className="server-list-control">
            <span>Status</span>
            <select
              value={statusFilter}
              onChange={(event) => updateStatus(event.target.value as 'all' | 'online' | 'offline')}
              className="server-list-select"
            >
              <option value="all">All</option>
              <option value="online">Online</option>
              <option value="offline">Offline</option>
            </select>
          </label>
        </div>

        {isLoadingServers ? (
          <StateNotice
            title="Loading server populations"
            description="Waiting for the backend to return the latest server list."
          />
        ) : null}

        {isRefreshingServers ? (
          <StateNotice
            title="Refreshing server populations"
            description="Reloading every server population and updated timestamp before showing the list again."
          />
        ) : null}

        {serversQuery.isError ? (
          <StateNotice
            title={hasServersData ? 'Refresh failed, showing cached results' : 'Server list request failed'}
            description={
              hasServersData
                ? 'The latest refresh did not complete, so the list below is still the last successful cached page.'
                : 'The backend did not return a cached server list for the homepage.'
            }
            detail={serverListErrorDetail}
            tone="error"
          />
        ) : null}

        {shouldShowServerData && servers.length > 0 ? (
          (() => {
            const pageData = serversPageData!

            return (
              <div className="server-list-summary">
                <div className="server-list-summary__stat">
                  <span className="server-list-summary__label">Visible servers</span>
                  <strong>{servers.length}</strong>
                </div>
                <div className="server-list-summary__stat">
                  <span className="server-list-summary__label">Total cached</span>
                  <strong>{pageData.totalItems}</strong>
                </div>
                <div className="server-list-summary__stat">
                  <span className="server-list-summary__label">Page size</span>
                  <strong>{pageData.pageSize}</strong>
                </div>
                <div className="server-list-summary__stat">
                  <span className="server-list-summary__label">Page</span>
                  <strong>
                    {pageData.page} / {totalPages}
                  </strong>
                </div>
              </div>
            )
          })()
        ) : null}

        {shouldShowServerData && hasSingleServer ? (
          <div className="server-focus">
            <div className="server-focus__content">
              <p className="page-text">
                One cached server is available. Open it directly and continue on the detail page.
              </p>
            </div>
            <Link to={`/servers/${servers[0].id}`} className="button-link">
              Open server
            </Link>
          </div>
        ) : null}

        {shouldShowServerData && servers.length > 0 ? (
          <div className="server-list-table" aria-label="Server list">
            <div className="server-list-table__header">
              <span>Name</span>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('players')}
              >
                Players{getSortIndicator('players')}
              </button>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('lastWipe')}
              >
                Last Wipe{getSortIndicator('lastWipe')}
              </button>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('nextWipe')}
              >
                Next Wipe{getSortIndicator('nextWipe')}
              </button>
              <span>Location</span>
              <span>Updated</span>
            </div>
            {servers.map((server) => (
              <ServerListCard key={server.id} server={server} />
            ))}
          </div>
        ) : null}

        {shouldShowServerData && servers.length === 0 ? (
          <StateNotice
            title="No servers available"
            description={
              debouncedSearch || statusValue !== undefined
                ? 'No cached servers match the current search or status filter.'
                : 'Start backend polling to populate the cached server list.'
            }
          />
        ) : null}

        {shouldShowServerData && totalPages > 1 ? (
          <div className="server-list-pagination">
            <button
              type="button"
              className="button-link button-link--secondary"
              onClick={() => setPage((current) => Math.max(1, current - 1))}
              disabled={page === 1 || serversQuery.isFetching}
            >
              Previous
            </button>
            <p className="page-text">
              Page {page} of {totalPages}
            </p>
            <button
              type="button"
              className="button-link button-link--secondary"
              onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
              disabled={page >= totalPages || serversQuery.isFetching}
            >
              Next
            </button>
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}
