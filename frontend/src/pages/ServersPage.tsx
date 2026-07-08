import { useMutation, useQuery } from '@tanstack/react-query'
import { Fragment, type FormEvent, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import { ApiError, getServerRefreshStatus, getServers, queueServerRefresh, serverStatus } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { ServerListCard } from '../features/servers/ServerListCard'
import { useNow } from '../hooks'

const pageSizeOptions = [10, 25, 50]
type ServerSortBy = 'players' | 'lastWipe' | 'nextWipe'
type ServerSortDirection = 'asc' | 'desc'

export function ServersPage() {
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | 'online' | 'offline'>('all')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [sortBy, setSortBy] = useState<ServerSortBy>('players')
  const [sortDirection, setSortDirection] = useState<ServerSortDirection>('desc')
  const [hideServerDataForRefresh, setHideServerDataForRefresh] = useState(false)
  const [refreshStatusPollingEnabled, setRefreshStatusPollingEnabled] = useState(false)
  const nowMs = useNow()
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

  const serverQueryParams = {
    search: appliedSearch || undefined,
    status: statusValue,
    page,
    pageSize,
    sortBy,
    sortDirection,
  }
  const serversQueryKey = ['servers', serverQueryParams] as const
  const serversQuery = useQuery({
    queryKey: serversQueryKey,
    queryFn: ({ signal }) => getServers(signal, serverQueryParams),
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })
  const refreshStatusQuery = useQuery({
    queryKey: ['server-refresh-status'],
    enabled: refreshStatusPollingEnabled,
    queryFn: ({ signal }) => getServerRefreshStatus(signal),
    staleTime: 0,
    refetchInterval: (query) => (
      query.state.data?.isQueuedOrRunning ? 1_000 : false
    ),
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })
  const queueRefreshMutation = useMutation({
    mutationFn: () => queueServerRefresh(),
    onMutate: () => {
      setHideServerDataForRefresh(true)
      setRefreshStatusPollingEnabled(false)
    },
    onSuccess: () => {
      setRefreshStatusPollingEnabled(true)
    },
    onError: () => {
      setHideServerDataForRefresh(false)
      setRefreshStatusPollingEnabled(false)
    },
  })
  const refetchServers = serversQuery.refetch

  useEffect(() => {
    const status = refreshStatusQuery.data
    if (hideServerDataForRefresh && status && !status.isQueuedOrRunning) {
      setHideServerDataForRefresh(false)
      setRefreshStatusPollingEnabled(false)
      void refetchServers()
    }
  }, [hideServerDataForRefresh, refreshStatusQuery.data, refetchServers])

  useEffect(() => {
    if (hideServerDataForRefresh && refreshStatusQuery.isError) {
      setHideServerDataForRefresh(false)
      setRefreshStatusPollingEnabled(false)
    }
  }, [hideServerDataForRefresh, refreshStatusQuery.isError])

  const serversPageData = serversQuery.data ?? null
  const servers = serversPageData?.items ?? []
  const hasSingleServer = servers.length === 1
  const totalItems = serversPageData?.totalItems ?? 0
  const totalPages = serversPageData ? Math.max(1, Math.ceil(totalItems / serversPageData.pageSize)) : 1
  const hasServersData = Boolean(serversPageData)
  const isLoadingServers = serversQuery.isFetching && !hasServersData
  const isRefreshingServers = hideServerDataForRefresh || (serversQuery.isFetching && hasServersData)
  const shouldShowServerData = hasServersData && !serversQuery.isFetching && !hideServerDataForRefresh
  const isRefreshButtonDisabled = queueRefreshMutation.isPending
    || refreshStatusQuery.data?.isQueuedOrRunning
    || hideServerDataForRefresh
  const serverListErrorDetail =
    serversQuery.error instanceof ApiError
      ? `${serversQuery.error.message} (${serversQuery.error.url})`
      : 'Unexpected error while loading the server list.'
  const refreshErrorDetail =
    queueRefreshMutation.error instanceof ApiError
      ? `${queueRefreshMutation.error.message} (${queueRefreshMutation.error.url})`
      : refreshStatusQuery.error instanceof ApiError
        ? `${refreshStatusQuery.error.message} (${refreshStatusQuery.error.url})`
        : refreshStatusQuery.data?.lastErrorMessage
          ? refreshStatusQuery.data.lastErrorMessage
          : 'Unexpected error while queuing the full server refresh.'

  function updateSearch(value: string) {
    setSearchInput(value)
  }

  function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setAppliedSearch(searchInput.trim())
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

  function getVisiblePageNumbers() {
    const pages = new Set<number>()
    const addPage = (pageNumber: number) => {
      if (pageNumber >= 1 && pageNumber <= totalPages) {
        pages.add(pageNumber)
      }
    }

    for (let pageNumber = 1; pageNumber <= 4; pageNumber += 1) {
      addPage(pageNumber)
    }

    for (let pageNumber = page - 1; pageNumber <= page + 1; pageNumber += 1) {
      addPage(pageNumber)
    }

    addPage(totalPages)

    return Array.from(pages).sort((left, right) => left - right)
  }

  return (
    <div className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">Server List</p>
        <h2 className="page-heading">Unofficial Rust server tracking</h2>
        <p className="page-text">
          Browse server activity, wipe timing, locations, and player counts in one survival-focused view.
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
              onClick={() => queueRefreshMutation.mutate()}
              disabled={isRefreshButtonDisabled}
            >
              {isRefreshButtonDisabled ? 'Refreshing...' : 'Refresh'}
            </button>
          </div>
        }
      >
        <form className="server-list-filters" onSubmit={submitSearch}>
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

          <button type="submit" className="button-link">
            Search
          </button>

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
        </form>

        {isLoadingServers ? (
          <StateNotice
            title="Loading server populations"
            description="Waiting for the backend to return the latest server list."
          />
        ) : null}

        {isRefreshingServers ? (
          <StateNotice
            title="Refreshing server populations"
            description="Refreshing all cached server populations before showing the sorted list again."
          />
        ) : null}

        {queueRefreshMutation.isError || refreshStatusQuery.isError || refreshStatusQuery.data?.lastErrorMessage ? (
          <StateNotice
            title="Refresh could not complete"
            description="Showing cached server data. The full refresh can be retried."
            detail={refreshErrorDetail}
            tone="error"
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
                  <span className="server-list-summary__label">Total servers</span>
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
              <ServerListCard key={server.id} server={server} nowMs={nowMs} />
            ))}
          </div>
        ) : null}

        {shouldShowServerData && servers.length === 0 ? (
          <StateNotice
            title="No servers available"
            description={
              appliedSearch || statusValue !== undefined
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
            <div className="server-list-pagination__pages" aria-label="Pagination">
              {getVisiblePageNumbers().map((pageNumber, index, visiblePages) => {
                const previousPageNumber = visiblePages[index - 1]
                const needsGap = previousPageNumber !== undefined && pageNumber - previousPageNumber > 1

                return (
                  <Fragment key={pageNumber}>
                    {needsGap ? <span className="server-list-pagination__gap">...</span> : null}
                    <button
                      type="button"
                      className={
                        pageNumber === page
                          ? 'server-list-pagination__page server-list-pagination__page--active'
                          : 'server-list-pagination__page'
                      }
                      onClick={() => setPage(pageNumber)}
                      disabled={pageNumber === page || serversQuery.isFetching}
                      aria-current={pageNumber === page ? 'page' : undefined}
                    >
                      {pageNumber}
                    </button>
                  </Fragment>
                )
              })}
            </div>
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
