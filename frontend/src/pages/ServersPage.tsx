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
import { formatRussianCount, type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

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
  { value: 'all', label: 'all' },
  { value: 'next24h', label: 'next24h' },
  { value: 'next7d', label: 'next7d' },
  { value: 'wiped24h', label: 'wiped24h' },
  { value: 'hasWipeData', label: 'hasWipeData' },
]
const serverListStaleTimeMs = 60 * 1000
const serverListCacheTimeMs = 10 * 60 * 1000

const wipeFilterLabels: Record<Language, Record<WipeFilter, string>> = {
  en: {
    all: 'All wipes',
    next24h: 'Wipes in 24h',
    next7d: 'Wipes in 7d',
    wiped24h: 'Wiped in 24h',
    hasWipeData: 'Has wipe data',
  },
  ru: {
    all: 'Все вайпы',
    next24h: 'Вайп в ближайшие 24 ч',
    next7d: 'Вайп в ближайшие 7 дн.',
    wiped24h: 'Вайп был за последние 24 ч',
    hasWipeData: 'Есть данные о вайпе',
  },
}

const serversPageCopy: Record<Language, {
  eyebrow: string
  heading: string
  intro: string
  sectionTitle: string
  sectionDescription: string
  pageSize: string
  refreshing: string
  refresh: string
  search: string
  searchPlaceholder: string
  status: string
  all: string
  online: string
  offline: string
  wipeTiming: string
  favorites: string
  serverTypeFilters: string
  loadingServerPopulations: string
  waitingForBackend: string
  loadingPage: (page: number) => string
  fetchingSelectedPage: string
  refreshingCachedServers: string
  readingLatestSavedData: string
  refreshFailedCached: string
  serverListRequestFailed: string
  refreshFailedDescription: string
  requestFailedDescription: string
  serversCount: (total: number) => string
  pageOf: (page: number, totalPages: number) => string
  showingOf: (visible: number, pageSize: number) => string
  oneCachedServer: string
  openServer: string
  table: {
    favorite: string
    rank: string
    name: string
    serverType: string
    players: string
    trend: string
    lastWipe: string
    nextWipe: string
    location: string
  }
  noFavoriteServers: string
  noServersAvailable: string
  favoriteDescription: string
  noFilterMatches: string
  startBackendPolling: string
  previous: string
  next: string
  unexpectedError: string
}> = {
  en: {
    eyebrow: 'Server List',
    heading: 'Unofficial Rust server tracking',
    intro: 'Browse server activity, wipe timing, locations, and player counts in one survival-focused view.',
    sectionTitle: 'Servers',
    sectionDescription: 'Find active Rust servers, compare player counts, and browse RustIQ Top 250 ranks by recent average population.',
    pageSize: 'Page size',
    refreshing: 'Refreshing...',
    refresh: 'Refresh',
    search: 'Search',
    searchPlaceholder: 'Search by name, host, or map',
    status: 'Status',
    all: 'All',
    online: 'Online',
    offline: 'Offline',
    wipeTiming: 'Wipe timing',
    favorites: 'Favorites',
    serverTypeFilters: 'Server type filters',
    loadingServerPopulations: 'Loading server populations',
    waitingForBackend: 'Waiting for the backend to return the latest server list.',
    loadingPage: (page) => `Loading page ${page}`,
    fetchingSelectedPage: 'Fetching servers for the selected page.',
    refreshingCachedServers: 'Refreshing cached servers',
    readingLatestSavedData: 'Reading the latest saved server data from the database.',
    refreshFailedCached: 'Refresh failed, showing cached results',
    serverListRequestFailed: 'Server list request failed',
    refreshFailedDescription: 'The latest refresh did not complete, so the list below is still the last successful cached page.',
    requestFailedDescription: 'The backend did not return a cached server list for the homepage.',
    serversCount: (total) => `${total} servers`,
    pageOf: (page, totalPages) => `Page ${page} of ${totalPages}`,
    showingOf: (visible, pageSize) => `Showing ${visible} of ${pageSize}`,
    oneCachedServer: 'One cached server is available. Open it directly and continue on the detail page.',
    openServer: 'Open server',
    table: {
      favorite: 'Favorite',
      rank: 'Rank',
      name: 'Name',
      serverType: 'Server Type',
      players: 'Players',
      trend: 'Trend',
      lastWipe: 'Last Wipe',
      nextWipe: 'Next Wipe',
      location: 'Location',
    },
    noFavoriteServers: 'No favorite servers on this page',
    noServersAvailable: 'No servers available',
    favoriteDescription: 'Favorite a server on this page or change pages to find saved servers that match the current filters.',
    noFilterMatches: 'No cached servers match the current search, status, or wipe filter.',
    startBackendPolling: 'Start backend polling to populate the cached server list.',
    previous: 'Previous',
    next: 'Next',
    unexpectedError: 'Unexpected error while loading the server list.',
  },
  ru: {
    eyebrow: 'Список серверов',
    heading: 'Неофициальный мониторинг серверов Rust',
    intro: 'Просматривайте активность серверов, вайпы, локации и онлайн игроков в одном удобном виде.',
    sectionTitle: 'Серверы',
    sectionDescription: 'Находите активные серверы Rust, сравнивайте онлайн и смотрите RustIQ Top 250 по недавнему среднему онлайну.',
    pageSize: 'Размер страницы',
    refreshing: 'Обновление...',
    refresh: 'Обновить',
    search: 'Поиск',
    searchPlaceholder: 'Поиск по названию, хосту или карте',
    status: 'Статус',
    all: 'Все',
    online: 'Онлайн',
    offline: 'Офлайн',
    wipeTiming: 'Вайпы',
    favorites: 'Избранное',
    serverTypeFilters: 'Фильтры типа сервера',
    loadingServerPopulations: 'Загрузка онлайна серверов',
    waitingForBackend: 'Ждем, пока серверная часть вернет последний список серверов.',
    loadingPage: (page) => `Загрузка страницы ${page}`,
    fetchingSelectedPage: 'Загрузка серверов для выбранной страницы.',
    refreshingCachedServers: 'Обновление кэша серверов',
    readingLatestSavedData: 'Чтение последних сохраненных данных серверов из базы.',
    refreshFailedCached: 'Обновление не удалось, показан кэш',
    serverListRequestFailed: 'Запрос списка серверов не удался',
    refreshFailedDescription: 'Последнее обновление не завершилось, поэтому ниже показана последняя успешно загруженная страница.',
    requestFailedDescription: 'Серверная часть не вернула кэшированный список серверов для главной страницы.',
    serversCount: (total) => formatRussianCount(total, 'сервер', 'сервера', 'серверов'),
    pageOf: (page, totalPages) => `Страница ${page} из ${totalPages}`,
    showingOf: (visible, pageSize) => `Показано ${visible} из ${pageSize}`,
    oneCachedServer: 'Доступен один кэшированный сервер. Откройте его напрямую и продолжите на странице деталей.',
    openServer: 'Открыть сервер',
    table: {
      favorite: 'Избранное',
      rank: 'Ранг',
      name: 'Название',
      serverType: 'Тип сервера',
      players: 'Игроки',
      trend: 'Тренд',
      lastWipe: 'Прошлый вайп',
      nextWipe: 'Следующий вайп',
      location: 'Локация',
    },
    noFavoriteServers: 'На этой странице нет избранных серверов',
    noServersAvailable: 'Серверы недоступны',
    favoriteDescription: 'Добавьте сервер в избранное на этой странице или смените страницу, чтобы найти сохраненные серверы с текущими фильтрами.',
    noFilterMatches: 'В кэше нет серверов, подходящих под текущий поиск, статус или фильтр вайпа.',
    startBackendPolling: 'Запустите опрос серверной части, чтобы заполнить кэшированный список серверов.',
    previous: 'Назад',
    next: 'Вперед',
    unexpectedError: 'Неожиданная ошибка при загрузке списка серверов.',
  },
}

function getServersQueryKey(params: GetServersParams) {
  return ['servers', params] as const
}

function getPageFromSearchParams(searchParams: URLSearchParams) {
  const pageParam = Number(searchParams.get('page'))

  return Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1
}

export function ServersPage() {
  const queryClient = useQueryClient()
  const { language } = useLanguage()
  const copy = serversPageCopy[language]
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
      : copy.unexpectedError
  const localizedWipeFilterOptions = useMemo(() => (
    wipeFilterOptions.map((option) => ({
      value: option.value,
      label: wipeFilterLabels[language][option.value],
    }))
  ), [language])

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
        <p className="page-eyebrow">{copy.eyebrow}</p>
        <h2 className="page-heading">{copy.heading}</h2>
        <p className="page-text">
          {copy.intro}
        </p>
      </div>

      <SectionCard
        title={copy.sectionTitle}
        description={copy.sectionDescription}
        actions={
          <div className="server-list-actions">
            <label className="server-list-control">
              <span>{copy.pageSize}</span>
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
              {serversQuery.isFetching ? copy.refreshing : copy.refresh}
            </button>
          </div>
        }
      >
        <div className="server-list-toolbar">
          <form className="server-list-filters" onSubmit={submitSearch}>
            <label className="server-list-control server-list-control--search">
              <span>{copy.search}</span>
              <input
                type="search"
                value={searchInput}
                onChange={(event) => updateSearch(event.target.value)}
                placeholder={copy.searchPlaceholder}
                className="server-list-input"
              />
            </label>

            <button type="submit" className="button-link">
              {copy.search}
            </button>

            <label className="server-list-control">
              <span>{copy.status}</span>
              <select
                value={statusFilter}
                onChange={(event) => updateStatus(event.target.value as 'all' | 'online' | 'offline')}
                className="server-list-select"
              >
                <option value="all">{copy.all}</option>
                <option value="online">{copy.online}</option>
                <option value="offline">{copy.offline}</option>
              </select>
            </label>

            <label className="server-list-control">
              <span>{copy.wipeTiming}</span>
              <select
                value={wipeFilter}
                onChange={(event) => updateWipeFilter(event.target.value as WipeFilter)}
                className="server-list-select"
              >
                {localizedWipeFilterOptions.map((option) => (
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
              {copy.favorites}
            </button>
          </form>

          <div className="server-type-filters" aria-label={copy.serverTypeFilters}>
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
                  {getRustServerTypeLabel(serverType, language)}
                </button>
              )
            })}
          </div>
        </div>

        {isLoadingServers ? (
          <StateNotice
            title={copy.loadingServerPopulations}
            description={copy.waitingForBackend}
          />
        ) : null}

        {isLoadingServerPage ? (
          <StateNotice
            title={copy.loadingPage(page)}
            description={copy.fetchingSelectedPage}
          />
        ) : null}

        {isRefreshingServers ? (
          <StateNotice
            title={copy.refreshingCachedServers}
            description={copy.readingLatestSavedData}
          />
        ) : null}

        {serversQuery.isError ? (
          <StateNotice
            title={hasServersData ? copy.refreshFailedCached : copy.serverListRequestFailed}
            description={
              hasServersData
                ? copy.refreshFailedDescription
                : copy.requestFailedDescription
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
                <span>{copy.serversCount(pageData.totalItems)}</span>
                <span>{copy.pageOf(page, totalPages)}</span>
                <span>{copy.showingOf(visibleServers.length, pageData.pageSize)}</span>
              </div>
            )
          })()
        ) : null}

        {shouldShowServerData && hasSingleServer ? (
          <div className="server-focus">
            <div className="server-focus__content">
              <p className="page-text">
                {copy.oneCachedServer}
              </p>
            </div>
            <Link to={`/servers/${visibleServers[0].id}`} className="button-link">
              {copy.openServer}
            </Link>
          </div>
        ) : null}

        {shouldShowServerData && visibleServers.length > 0 ? (
            <div className="server-list-table" aria-label="Server list">
            <div className="server-list-table__header">
              <span>{copy.table.favorite}</span>
              <button
                type="button"
                className="server-list-table__sort server-list-table__sort--rank"
                onClick={() => updateSort('rank')}
              >
                {copy.table.rank}{getSortIndicator('rank')}
              </button>
              <span>{copy.table.name}</span>
              <span>{copy.table.serverType}</span>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('players')}
              >
                {copy.table.players}{getSortIndicator('players')}
              </button>
              <span>{copy.table.trend}</span>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('lastWipe')}
              >
                {copy.table.lastWipe}{getSortIndicator('lastWipe')}
              </button>
              <button
                type="button"
                className="server-list-table__sort"
                onClick={() => updateSort('nextWipe')}
              >
                {copy.table.nextWipe}{getSortIndicator('nextWipe')}
              </button>
              <span>{copy.table.location}</span>
            </div>
            {visibleServers.map((server) => (
              <ServerListCard
                key={server.id}
                server={server}
                nowMs={nowMs}
                isFavorite={favoriteServerIdSet.has(server.id)}
                language={language}
                onToggleFavorite={toggleFavoriteServer}
              />
            ))}
          </div>
        ) : null}

        {shouldShowServerData && visibleServers.length === 0 && !isLoadingServerPage ? (
          <StateNotice
            title={showFavoritesOnly && servers.length > 0 ? copy.noFavoriteServers : copy.noServersAvailable}
            description={
              showFavoritesOnly && servers.length > 0
                ? copy.favoriteDescription
                : appliedSearch || statusValue !== undefined || wipeFilter !== 'all'
                ? copy.noFilterMatches
                : copy.startBackendPolling
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
              {copy.previous}
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
              {copy.next}
            </button>
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}
