import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { Fragment, type FormEvent, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'

import {
  ApiError,
  type GetServersParams,
  getServers,
  rustServerType,
  serverStatus,
  type RustServerType,
} from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { ServerListCard } from '../features/servers/ServerListCard'
import { getRustServerTypeLabel } from '../features/servers/serverPresentation'
import { useFavoriteServers } from '../features/servers/useFavoriteServers'
import { useNow } from '../hooks'

const pageSizeOptions = [10, 25, 50]
type ServerSortBy = 'players' | 'rank' | 'lastWipe' | 'nextWipe'
type ServerSortDirection = 'asc' | 'desc'
type WipeFilter = 'all' | 'next24h' | 'next7d' | 'wiped24h' | 'hasWipeData'
const serverTypeFilterOptions = [
  rustServerType.Official,
  rustServerType.Community,
  rustServerType.Modded,
] as const
const wipeFilterOptions: Array<{ value: WipeFilter, label: string }> = [
  { value: 'all', label: 'All wipes' },
  { value: 'next24h', label: 'Wipes in 24h' },
  { value: 'next7d', label: 'Wipes in 7d' },
  { value: 'wiped24h', label: 'Wiped in 24h' },
  { value: 'hasWipeData', label: 'Has wipe data' },
]
const serverListStaleTimeMs = 2 * 60 * 1000
const serverListCacheTimeMs = 10 * 60 * 1000

function getServersQueryKey(params: GetServersParams) {
  return ['servers', params] as const
}

function getPageFromSearchParams(searchParams: URLSearchParams) {
  const pageParam = Number(searchParams.get('page'))

  return Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1
}

export function ServersPage() {
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const [searchInput, setSearchInput] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | 'online' | 'offline'>('all')
  const [serverTypeFilters, setServerTypeFilters] = useState<RustServerType[]>([])
  const [wipeFilter, setWipeFilter] = useState<WipeFilter>('all')
  const [showFavoritesOnly, setShowFavoritesOnly] = useState(false)
  const [page, setPageState] = useState(() => getPageFromSearchParams(searchParams))
  const [pageSize, setPageSize] = useState(10)
  const [sortBy, setSortBy] = useState<ServerSortBy>('players')
  const [sortDirection, setSortDirection] = useState<ServerSortDirection>('desc')
  const {
    favoriteServerIdSet,
    toggleFavoriteServer,
  } = useFavoriteServers()
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

  const serverQueryBaseParams = useMemo(() => ({
    search: appliedSearch || undefined,
    status: statusValue,
    serverTypes: serverTypeFilters,
    pageSize,
    sortBy,
    sortDirection,
    wipeFilter: wipeFilter === 'all' ? undefined : wipeFilter,
  }), [
    appliedSearch,
    pageSize,
    serverTypeFilters,
    sortBy,
    sortDirection,
    statusValue,
    wipeFilter,
  ])
  const serverQueryParams = useMemo(() => ({
    ...serverQueryBaseParams,
    page,
  }), [page, serverQueryBaseParams])
  const serversQueryKey = getServersQueryKey(serverQueryParams)
  const serversQuery = useQuery({
    queryKey: serversQueryKey,
    queryFn: ({ signal }) => getServers(signal, serverQueryParams),
    staleTime: serverListStaleTimeMs,
    gcTime: serverListCacheTimeMs,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  })

  const serversPageData = serversQuery.data ?? null
  const servers = serversPageData?.items ?? []
  const visibleServers = showFavoritesOnly
    ? servers.filter((server) => favoriteServerIdSet.has(server.id))
    : servers
  const hasSingleServer = visibleServers.length === 1
  const totalItems = serversPageData?.totalItems ?? 0
  const totalPages = serversPageData ? Math.max(1, Math.ceil(totalItems / serversPageData.pageSize)) : 1
  const hasServersData = Boolean(serversPageData)
  const isLoadingServers = serversQuery.isFetching && !hasServersData
  const isLoadingServerPage = serversQuery.isFetching && serversQuery.isPlaceholderData
  const isRefreshingServers = serversQuery.isFetching && hasServersData && !serversQuery.isPlaceholderData
  const shouldShowServerData = hasServersData
  const isRefreshButtonDisabled = serversQuery.isFetching
  const serverListErrorDetail =
    serversQuery.error instanceof ApiError
      ? `${serversQuery.error.message} (${serversQuery.error.url})`
      : 'Unexpected error while loading the server list.'

  useEffect(() => {
    const pageFromSearchParams = getPageFromSearchParams(searchParams)

    if (pageFromSearchParams !== page) {
      setPageState(pageFromSearchParams)
    }
  }, [page, searchParams])

  useEffect(() => {
    if (!hasServersData || totalPages <= 1) {
      return
    }

    const pagesToPrefetch = [page - 1, page + 1]
      .filter((pageNumber) => pageNumber >= 1 && pageNumber <= totalPages)

    for (const pageNumber of pagesToPrefetch) {
      const params = {
        ...serverQueryBaseParams,
        page: pageNumber,
      }

      void queryClient.prefetchQuery({
        queryKey: getServersQueryKey(params),
        queryFn: ({ signal }) => getServers(signal, params),
        staleTime: serverListStaleTimeMs,
        gcTime: serverListCacheTimeMs,
      })
    }
  }, [hasServersData, page, queryClient, serverQueryBaseParams, totalPages])

  function setPage(nextPageOrUpdater: number | ((currentPage: number) => number)) {
    const nextPage = typeof nextPageOrUpdater === 'function'
      ? nextPageOrUpdater(page)
      : nextPageOrUpdater
    const normalizedPage = Math.max(1, nextPage)

    setPageState(normalizedPage)
    setSearchParams((currentSearchParams) => {
      const nextSearchParams = new URLSearchParams(currentSearchParams)

      if (normalizedPage === 1) {
        nextSearchParams.delete('page')
      } else {
        nextSearchParams.set('page', String(normalizedPage))
      }

      return nextSearchParams
    })
  }

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

  function toggleServerTypeFilter(serverType: RustServerType) {
    setServerTypeFilters((currentFilters) => (
      currentFilters.includes(serverType)
        ? currentFilters.filter((currentFilter) => currentFilter !== serverType)
        : [...currentFilters, serverType]
    ))
    setPage(1)
  }

  function updateWipeFilter(nextWipeFilter: WipeFilter) {
    setWipeFilter(nextWipeFilter)
    setPage(1)
  }

  function toggleFavoritesOnly() {
    setShowFavoritesOnly((currentValue) => !currentValue)
    setPage(1)
  }

  function updatePageSize(value: number) {
    setPageSize(value)
    setPage(1)
  }

  function updateSort(nextSortBy: ServerSortBy) {
    const defaultDirection = nextSortBy === 'nextWipe' || nextSortBy === 'rank' ? 'asc' : 'desc'
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
    <div className="page-stack server-browser-page">
      <div className="page-copy">
        <p className="page-eyebrow">Server List</p>
        <h2 className="page-heading">Unofficial Rust server tracking</h2>
        <p className="page-text">
          Browse server activity, wipe timing, locations, and player counts in one survival-focused view.
        </p>
      </div>

      <SectionCard
        title="Servers"
        description="Find active Rust servers, compare player counts, and browse RustIQ Top 250 ranks by recent average population."
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
              onClick={() => void serversQuery.refetch()}
              disabled={isRefreshButtonDisabled}
            >
              {serversQuery.isFetching ? 'Refreshing...' : 'Refresh'}
            </button>
          </div>
        }
      >
        <div className="server-list-toolbar">
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

            <label className="server-list-control">
              <span>Wipe timing</span>
              <select
                value={wipeFilter}
                onChange={(event) => updateWipeFilter(event.target.value as WipeFilter)}
                className="server-list-select"
              >
                {wipeFilterOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              className={showFavoritesOnly ? 'server-filter-toggle server-filter-toggle--active' : 'server-filter-toggle'}
              onClick={toggleFavoritesOnly}
              aria-pressed={showFavoritesOnly}
            >
              Favorites
            </button>
          </form>

          <div className="server-type-filters" aria-label="Server type filters">
            {serverTypeFilterOptions.map((serverType) => {
              const isActive = serverTypeFilters.includes(serverType)

              return (
                <button
                  key={serverType}
                  type="button"
                  className={isActive ? 'server-type-filter server-type-filter--active' : 'server-type-filter'}
                  onClick={() => toggleServerTypeFilter(serverType)}
                  aria-pressed={isActive}
                >
                  {getRustServerTypeLabel(serverType)}
                </button>
              )
            })}
          </div>
        </div>

        {isLoadingServers ? (
          <StateNotice
            title="Loading server populations"
            description="Waiting for the backend to return the latest server list."
          />
        ) : null}

        {isLoadingServerPage ? (
          <StateNotice
            title={`Loading page ${page}`}
            description="Fetching servers for the selected page."
          />
        ) : null}

        {isRefreshingServers ? (
          <StateNotice
            title="Refreshing cached servers"
            description="Reading the latest saved server data from the database."
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

        {shouldShowServerData && visibleServers.length > 0 ? (
          (() => {
            const pageData = serversPageData!

            return (
              <div className="server-list-summary">
                <span>{pageData.totalItems} servers</span>
                <span>Page {page} of {totalPages}</span>
                <span>Showing {visibleServers.length} of {pageData.pageSize}</span>
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
            <Link to={`/servers/${visibleServers[0].id}`} className="button-link">
              Open server
            </Link>
          </div>
        ) : null}

        {shouldShowServerData && visibleServers.length > 0 ? (
            <div className="server-list-table" aria-label="Server list">
            <div className="server-list-table__header">
              <span>Favorite</span>
              <button
                type="button"
                className="server-list-table__sort server-list-table__sort--rank"
                onClick={() => updateSort('rank')}
              >
                Rank{getSortIndicator('rank')}
              </button>
              <span>Name</span>
              <span>Server Type</span>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('players')}
              >
                Players{getSortIndicator('players')}
              </button>
              <span>Trend</span>
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
            </div>
            {visibleServers.map((server) => (
              <ServerListCard
                key={server.id}
                server={server}
                nowMs={nowMs}
                isFavorite={favoriteServerIdSet.has(server.id)}
                onToggleFavorite={toggleFavoriteServer}
              />
            ))}
          </div>
        ) : null}

        {shouldShowServerData && visibleServers.length === 0 && !isLoadingServerPage ? (
          <StateNotice
            title={showFavoritesOnly && servers.length > 0 ? 'No favorite servers on this page' : 'No servers available'}
            description={
              showFavoritesOnly && servers.length > 0
                ? 'Favorite a server on this page or change pages to find saved servers that match the current filters.'
                : appliedSearch || statusValue !== undefined || wipeFilter !== 'all'
                ? 'No cached servers match the current search, status, or wipe filter.'
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
              disabled={page === 1}
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
                      disabled={pageNumber === page}
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
              disabled={page >= totalPages}
            >
              Next
            </button>
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}
