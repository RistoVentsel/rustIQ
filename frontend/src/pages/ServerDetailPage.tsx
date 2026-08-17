import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'

import { ApiError, getServerById, getServerMap, getServerPlayers } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { ServerLayoutPreview } from '../features/servers/ServerLayoutPreview'
import {
  formatClientConnectCommand,
  formatConnectAddress,
  formatCountry,
  formatServerLocation,
  formatPlayerCount,
  formatServerSubtitle,
  formatServerTimestamp,
  getRustServerTypeClassName,
  getRustServerTypeLabel,
  getServerDisplayName,
  getServerStatusLabel,
} from '../features/servers/serverPresentation'
import { useServerLiveUpdates } from '../features/servers/useServerLiveUpdates'
import { useAdsGate, useNow } from '../hooks'
import type { Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'
import { formatCoarseRelativeTime, getSecondsAgo } from '../lib/time'

const serverDetailCopy: Record<Language, {
  liveStatus: {
    connected: string
    reconnecting: string
    connecting: string
    error: string
    disconnected: string
    idle: string
  }
  location: (country: string) => string
  unexpectedError: string
  unknown: string
  unavailable: string
  copied: string
  copy: string
  copyAddress: (address: string) => string
  copyConsole: (command: string) => string
  pageEyebrow: string
  pageHeading: string
  pageIntro: string
  overviewTitle: string
  overviewDescription: string
  livePlayercount: string
  back: string
  serverDetailsNotFound: string
  connectingLiveTitle: string
  connectingLiveDescription: string
  reconnectingLiveTitle: string
  reconnectingLiveDescription: string
  liveUnavailableTitle: string
  liveUnavailableDescription: string
  loadingDetailsTitle: string
  loadingDetailsDescription: string
  refreshFailedTitle: string
  detailRequestFailedTitle: string
  refreshFailedDescription: string
  stats: {
    players: string
    serverType: string
    lastSeen: string
    lastWiped: string
    nextWipe: string
    location: string
  }
  details: {
    connectionAddress: string
    rustConsole: string
    serverType: string
    lastWiped: string
    nextWipe: string
    mapSummary: string
  }
  mapTitle: string
  mapDescription: string
  loadingMapTitle: string
  loadingMapDescription: string
  refreshingMapTitle: string
  refreshingMapDescription: string
  mapRefreshFailedTitle: string
  mapUnavailableTitle: string
  mapRefreshFailedDescription: string
  mapDetailsUnavailableDescription: string
  mapThumbnailUnavailableTitle: string
  mapThumbnailUnavailableDescription: string
  mapThumbnailAlt: string
  mapName: string
  seed: string
  size: string
  fetched: string
  openRustMaps: string
  mapFile: string
  downloadMapFile: string
  playersTitle: string
  playersDescription: string
  loadingPlayersTitle: string
  loadingPlayersDescription: string
  refreshingPlayersTitle: string
  refreshingPlayersDescription: string
  playerRefreshFailedTitle: string
  playerRequestFailedTitle: string
  playerRefreshFailedDescription: string
  noPlayersTitle: string
  playersUnavailableTitle: string
  noPlayersDescription: string
  playersUnavailableDescription: string
  unnamedPlayer: string
  duration: string
}> = {
  en: {
    liveStatus: {
      connected: 'Live connected',
      reconnecting: 'Reconnecting',
      connecting: 'Connecting',
      error: 'Live error',
      disconnected: 'Disconnected',
      idle: 'Live idle',
    },
    location: (country) => `Location ${country}`,
    unexpectedError: 'Unexpected error while loading server data.',
    unknown: 'Unknown',
    unavailable: 'Unavailable',
    copied: 'Copied',
    copy: 'Copy',
    copyAddress: (address) => `Copy connection address ${address}`,
    copyConsole: (command) => `Copy Rust console command ${command}`,
    pageEyebrow: 'Server Detail',
    pageHeading: 'Server details',
    pageIntro: 'View the latest population, map, wipe schedule, and live player details for this server.',
    overviewTitle: 'Server overview',
    overviewDescription: 'Current status, location, wipe timing, and connection details.',
    livePlayercount: 'Live playercount',
    back: 'Back',
    serverDetailsNotFound: 'Server details could not be found',
    connectingLiveTitle: 'Connecting to live updates',
    connectingLiveDescription: 'The page is opening its SignalR subscription for this server.',
    reconnectingLiveTitle: 'Reconnecting live updates',
    reconnectingLiveDescription: 'Live pushes paused temporarily. Cached server data is still visible while SignalR reconnects.',
    liveUnavailableTitle: 'Live updates unavailable',
    liveUnavailableDescription: 'The page could not keep the SignalR subscription active. Cached REST data is still shown below.',
    loadingDetailsTitle: 'Loading server details',
    loadingDetailsDescription: 'Loading the latest saved details for this server.',
    refreshFailedTitle: 'Server refresh failed, showing cached details',
    detailRequestFailedTitle: 'Server detail request failed',
    refreshFailedDescription: 'The latest detail refresh failed, so the page is still using the last successful server snapshot.',
    stats: {
      players: 'Players',
      serverType: 'Server type',
      lastSeen: 'Last seen',
      lastWiped: 'Last wiped',
      nextWipe: 'Next wipe',
      location: 'Location',
    },
    details: {
      connectionAddress: 'Connection address',
      rustConsole: 'Rust console',
      serverType: 'Server type',
      lastWiped: 'Last wiped',
      nextWipe: 'Next wipe',
      mapSummary: 'Map summary',
    },
    mapTitle: 'Map',
    mapDescription: 'Map preview, seed, size, and RustMaps links when available.',
    loadingMapTitle: 'Loading map details',
    loadingMapDescription: 'Checking whether this server has a saved map preview.',
    refreshingMapTitle: 'Refreshing map details',
    refreshingMapDescription: 'Showing the current map while newer details load.',
    mapRefreshFailedTitle: 'Map refresh failed, showing saved details',
    mapUnavailableTitle: 'Map details unavailable',
    mapRefreshFailedDescription: 'The newest map details could not be loaded, so this section is still showing the last saved version.',
    mapDetailsUnavailableDescription: 'No map preview or RustMaps details have been saved for this server yet.',
    mapThumbnailUnavailableTitle: 'Map thumbnail unavailable',
    mapThumbnailUnavailableDescription: 'Showing text-only map details because no thumbnail image is available for this server.',
    mapThumbnailAlt: 'Server map thumbnail',
    mapName: 'Map name',
    seed: 'Seed',
    size: 'Size',
    fetched: 'Fetched',
    openRustMaps: 'Open RustMaps',
    mapFile: 'Map file',
    downloadMapFile: 'Download map file',
    playersTitle: 'Current players',
    playersDescription: 'See who is online right now when the server shares player names.',
    loadingPlayersTitle: 'Loading current players',
    loadingPlayersDescription: 'Checking whether the server is sharing its current player list.',
    refreshingPlayersTitle: 'Refreshing current players',
    refreshingPlayersDescription: 'Showing the latest player list while newer details load.',
    playerRefreshFailedTitle: 'Player list refresh failed, showing cached players',
    playerRequestFailedTitle: 'Current players request failed',
    playerRefreshFailedDescription: 'The newest player list could not be loaded, so this section is still using the last successful response.',
    noPlayersTitle: 'No players currently listed',
    playersUnavailableTitle: 'Current player list unavailable',
    noPlayersDescription: 'This server is not currently sharing any named players.',
    playersUnavailableDescription: 'This server has not shared a current player list yet.',
    unnamedPlayer: 'Unnamed player',
    duration: 'Duration',
  },
  ru: {
    liveStatus: {
      connected: 'Live-соединение активно',
      reconnecting: 'Переподключение',
      connecting: 'Подключение',
      error: 'Ошибка Live',
      disconnected: 'Отключено',
      idle: 'Live в ожидании',
    },
    location: (country) => `Локация ${country}`,
    unexpectedError: 'Неожиданная ошибка при загрузке данных сервера.',
    unknown: 'Неизвестно',
    unavailable: 'Недоступно',
    copied: 'Скопировано',
    copy: 'Копировать',
    copyAddress: (address) => `Копировать адрес подключения ${address}`,
    copyConsole: (command) => `Копировать команду консоли Rust ${command}`,
    pageEyebrow: 'Детали сервера',
    pageHeading: 'Детали сервера',
    pageIntro: 'Смотрите последний онлайн, карту, расписание вайпов и Live-данные игроков для этого сервера.',
    overviewTitle: 'Обзор сервера',
    overviewDescription: 'Текущий статус, локация, вайпы и данные подключения.',
    livePlayercount: 'Live-онлайн',
    back: 'Назад',
    serverDetailsNotFound: 'Детали сервера не найдены',
    connectingLiveTitle: 'Подключение к Live-обновлениям',
    connectingLiveDescription: 'Страница открывает подписку SignalR для этого сервера.',
    reconnectingLiveTitle: 'Переподключение Live-обновлений',
    reconnectingLiveDescription: 'Live-обновления временно остановлены. Кэшированные данные сервера остаются видимыми.',
    liveUnavailableTitle: 'Live-обновления недоступны',
    liveUnavailableDescription: 'Страница не смогла удержать подписку SignalR. Ниже показаны кэшированные REST-данные.',
    loadingDetailsTitle: 'Загрузка деталей сервера',
    loadingDetailsDescription: 'Загружаем последние сохраненные детали для этого сервера.',
    refreshFailedTitle: 'Обновление сервера не удалось, показан кэш',
    detailRequestFailedTitle: 'Запрос деталей сервера не удался',
    refreshFailedDescription: 'Последнее обновление деталей не удалось, поэтому страница использует последний успешный снимок сервера.',
    stats: {
      players: 'Игроки',
      serverType: 'Тип сервера',
      lastSeen: 'Последняя активность',
      lastWiped: 'Прошлый вайп',
      nextWipe: 'Следующий вайп',
      location: 'Локация',
    },
    details: {
      connectionAddress: 'Адрес подключения',
      rustConsole: 'Консоль Rust',
      serverType: 'Тип сервера',
      lastWiped: 'Прошлый вайп',
      nextWipe: 'Следующий вайп',
      mapSummary: 'Сводка карты',
    },
    mapTitle: 'Карта',
    mapDescription: 'Превью карты, seed, размер и ссылки RustMaps, если доступны.',
    loadingMapTitle: 'Загрузка деталей карты',
    loadingMapDescription: 'Проверяем, есть ли сохраненное превью карты для этого сервера.',
    refreshingMapTitle: 'Обновление деталей карты',
    refreshingMapDescription: 'Показываем текущую карту, пока загружаются новые данные.',
    mapRefreshFailedTitle: 'Обновление карты не удалось, показан кэш',
    mapUnavailableTitle: 'Детали карты недоступны',
    mapRefreshFailedDescription: 'Новые данные карты не загрузились, поэтому раздел показывает последнюю сохраненную версию.',
    mapDetailsUnavailableDescription: 'Для этого сервера пока нет сохраненного превью карты или данных RustMaps.',
    mapThumbnailUnavailableTitle: 'Превью карты недоступно',
    mapThumbnailUnavailableDescription: 'Показаны только текстовые детали карты, потому что изображения для этого сервера нет.',
    mapThumbnailAlt: 'Превью карты сервера',
    mapName: 'Название карты',
    seed: 'Seed',
    size: 'Размер',
    fetched: 'Получено',
    openRustMaps: 'Открыть RustMaps',
    mapFile: 'Файл карты',
    downloadMapFile: 'Скачать файл карты',
    playersTitle: 'Текущие игроки',
    playersDescription: 'Смотрите, кто сейчас онлайн, если сервер передает имена игроков.',
    loadingPlayersTitle: 'Загрузка текущих игроков',
    loadingPlayersDescription: 'Проверяем, передает ли сервер текущий список игроков.',
    refreshingPlayersTitle: 'Обновление текущих игроков',
    refreshingPlayersDescription: 'Показываем последний список игроков, пока загружаются новые данные.',
    playerRefreshFailedTitle: 'Обновление списка игроков не удалось, показан кэш',
    playerRequestFailedTitle: 'Запрос текущих игроков не удался',
    playerRefreshFailedDescription: 'Новый список игроков не загрузился, поэтому раздел использует последний успешный ответ.',
    noPlayersTitle: 'Сейчас нет списка игроков',
    playersUnavailableTitle: 'Список текущих игроков недоступен',
    noPlayersDescription: 'Этот сервер сейчас не передает имена игроков.',
    playersUnavailableDescription: 'Этот сервер еще не передавал текущий список игроков.',
    unnamedPlayer: 'Игрок без имени',
    duration: 'Длительность',
  },
}

function getLiveStatusLabel(status: ReturnType<typeof useServerLiveUpdates>, language: Language) {
  const copy = serverDetailCopy[language].liveStatus

  switch (status) {
    case 'connected':
      return copy.connected
    case 'reconnecting':
      return copy.reconnecting
    case 'connecting':
      return copy.connecting
    case 'error':
      return copy.error
    case 'disconnected':
      return copy.disconnected
    default:
      return copy.idle
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

function getQueryErrorDetail(error: unknown, fallback: string) {
  return error instanceof ApiError
    ? `${error.message} (${error.url})`
    : fallback
}

function formatPlayerDuration(durationSeconds: number, language: Language) {
  const totalMinutes = Math.max(0, Math.round(durationSeconds / 60))
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  const hourSuffix = language === 'ru' ? 'ч' : 'h'
  const minuteSuffix = language === 'ru' ? 'мин' : 'm'

  if (hours > 0 && minutes > 0) {
    return `${hours}${hourSuffix} ${minutes}${minuteSuffix}`
  }

  if (hours > 0) {
    return `${hours}${hourSuffix}`
  }

  return `${minutes}${minuteSuffix}`
}

type CopiedServerDetailField = 'address' | 'connect'

function ServerDetailLocation({
  server,
  language,
}: {
  server: Parameters<typeof formatServerLocation>[0]
  language: Language
}) {
  const copy = serverDetailCopy[language]
  const country = formatCountry(server.country, language)
  const countryCode = server.country?.trim().toLowerCase()
  const flagSrc = countryCode && /^[a-z]{2}$/.test(countryCode)
    ? `https://flagcdn.com/w40/${countryCode}.png`
    : null

  return (
    <span className="server-detail-location" aria-label={copy.location(formatCountry(server.country, language))}>
      <span className="server-location__initials">{country}</span>
      {flagSrc ? (
        <img className="server-location__flag" src={flagSrc} alt="" loading="lazy" />
      ) : (
        <span className="server-location__flag server-location__flag--empty" aria-hidden="true" />
      )}
    </span>
  )
}

export function ServerDetailPage() {
  const { serverId } = useParams<{ serverId: string }>()
  const navigate = useNavigate()
  const { language } = useLanguage()
  const copy = serverDetailCopy[language]
  const liveStatus = useServerLiveUpdates(serverId)
  const nowMs = useNow()
  const [copiedServerDetailField, setCopiedServerDetailField] = useState<CopiedServerDetailField | null>(null)
  const serverQuery = useQuery({
    queryKey: ['server', serverId],
    enabled: Boolean(serverId),
    queryFn: ({ signal }) => getServerById(serverId!, signal),
    staleTime: 10_000,
    refetchOnMount: 'always',
    retry: false,
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
  const players = playersQuery.data?.players ?? []
  const hasPlayerSnapshot = Boolean(playersQuery.data?.refreshedAtUtc)
  const hasServerData = Boolean(serverQuery.data)
  useAdsGate(hasServerData)
  const lastSeenSecondsAgo = serverQuery.data?.lastSeenAtUtc
    ? getSecondsAgo(serverQuery.data.lastSeenAtUtc, nowMs)
    : null
  const serverConnectAddress = serverQuery.data ? formatConnectAddress(serverQuery.data) : ''
  const serverConnectCommand = serverQuery.data ? formatClientConnectCommand(serverQuery.data) : ''
  const serverTypeLabel = serverQuery.data
    ? getRustServerTypeLabel(serverQuery.data.serverType, language)
    : copy.unknown

  async function copyServerDetailText(text: string, field: CopiedServerDetailField) {
    await navigator.clipboard.writeText(text)
    setCopiedServerDetailField(field)
    window.setTimeout(() => {
      setCopiedServerDetailField((currentField) => currentField === field ? null : currentField)
    }, 1600)
  }

  function navigateBackToServerList() {
    if (window.history.state?.idx > 0) {
      navigate(-1)
      return
    }

    navigate('/')
  }

  return (
    <div className="page-stack server-detail-page">
      <div className="page-copy">
        <p className="page-eyebrow">{copy.pageEyebrow}</p>
        <h2 className="page-heading">{copy.pageHeading}</h2>
        <p className="page-text">
          {copy.pageIntro}
        </p>
      </div>

      <SectionCard
        title={copy.overviewTitle}
        description={copy.overviewDescription}
        actions={
          <div className="server-detail-actions">
            <span className={getLiveStatusClassName(liveStatus)}>{getLiveStatusLabel(liveStatus, language)}</span>
            <span className="status-pill status-pill--online">{copy.livePlayercount}</span>
            <button
              type="button"
              className="button-link button-link--secondary"
              onClick={navigateBackToServerList}
            >
              {copy.back}
            </button>
          </div>
        }
      >
        {!serverId ? (
          <StateNotice title={copy.serverDetailsNotFound} tone="error" />
        ) : null}

        {liveStatus === 'connecting' ? (
          <StateNotice
            title={copy.connectingLiveTitle}
            description={copy.connectingLiveDescription}
            tone="info"
          />
        ) : null}

        {liveStatus === 'reconnecting' ? (
          <StateNotice
            title={copy.reconnectingLiveTitle}
            description={copy.reconnectingLiveDescription}
            tone="warning"
          />
        ) : null}

        {liveStatus === 'disconnected' || liveStatus === 'error' ? (
          <StateNotice
            title={copy.liveUnavailableTitle}
            description={copy.liveUnavailableDescription}
            tone="error"
          />
        ) : null}

        {serverQuery.isLoading && !hasServerData ? (
          <StateNotice
            title={copy.loadingDetailsTitle}
            description={copy.loadingDetailsDescription}
          />
        ) : null}

        {serverQuery.isError ? (
          <StateNotice
            title={hasServerData ? copy.refreshFailedTitle : copy.detailRequestFailedTitle}
            description={
              hasServerData
                ? copy.refreshFailedDescription
                : undefined
            }
            detail={getQueryErrorDetail(serverQuery.error, copy.unexpectedError)}
            tone="error"
          />
        ) : null}

        {serverQuery.data ? (
          <div className="page-stack">
            <ServerLayoutPreview
              name={getServerDisplayName(serverQuery.data)}
              subtitle={formatServerSubtitle(serverQuery.data)}
              badgeLabel={getServerStatusLabel(serverQuery.data.status, language)}
              stats={[
                { label: copy.stats.players, value: formatPlayerCount(serverQuery.data) },
                {
                  label: copy.stats.serverType,
                  value: (
                    <span className={getRustServerTypeClassName(serverQuery.data.serverType)}>
                      {serverTypeLabel}
                    </span>
                  ),
                },
                {
                  label: copy.stats.lastSeen,
                  value: formatCoarseRelativeTime(lastSeenSecondsAgo, language),
                },
                { label: copy.stats.lastWiped, value: formatServerTimestamp(serverQuery.data.lastWipeAtUtc, language) },
                { label: copy.stats.nextWipe, value: formatServerTimestamp(serverQuery.data.nextWipeAtUtc, language) },
                { label: copy.stats.location, value: <ServerDetailLocation server={serverQuery.data} language={language} /> },
              ]}
            />

            <dl className="server-detail-meta">
              <div className="server-detail-meta__item">
                <dt>{copy.details.connectionAddress}</dt>
                <dd className="server-detail-address">
                  <span>{serverConnectAddress}</span>
                  <button
                    type="button"
                    className="copy-button"
                    onClick={() => {
                      void copyServerDetailText(serverConnectAddress, 'address')
                    }}
                    aria-label={copy.copyAddress(serverConnectAddress)}
                  >
                    {copiedServerDetailField === 'address' ? copy.copied : copy.copy}
                  </button>
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.details.rustConsole}</dt>
                <dd className="server-detail-address">
                  <code className="server-detail-command">{serverConnectCommand}</code>
                  <button
                    type="button"
                    className="copy-button"
                    onClick={() => {
                      void copyServerDetailText(serverConnectCommand, 'connect')
                    }}
                    aria-label={copy.copyConsole(serverConnectCommand)}
                  >
                    {copiedServerDetailField === 'connect' ? copy.copied : copy.copy}
                  </button>
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.details.serverType}</dt>
                <dd>
                  <span className={getRustServerTypeClassName(serverQuery.data.serverType)}>
                    {serverTypeLabel}
                  </span>
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.details.lastWiped}</dt>
                <dd>{formatServerTimestamp(serverQuery.data.lastWipeAtUtc, language)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.details.nextWipe}</dt>
                <dd>{formatServerTimestamp(serverQuery.data.nextWipeAtUtc, language)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.details.mapSummary}</dt>
                <dd>{serverQuery.data.mapMetadata?.mapName ?? serverQuery.data.map ?? copy.unavailable}</dd>
              </div>
            </dl>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard
        title={copy.mapTitle}
        description={copy.mapDescription}
      >
        {mapQuery.isLoading && mapQuery.data === undefined ? (
          <StateNotice
            title={copy.loadingMapTitle}
            description={copy.loadingMapDescription}
          />
        ) : null}

        {mapQuery.isFetching && mapQuery.data !== undefined ? (
          <StateNotice
            title={copy.refreshingMapTitle}
            description={copy.refreshingMapDescription}
          />
        ) : null}

        {mapQuery.isError ? (
          <StateNotice
            title={mapQuery.data !== undefined ? copy.mapRefreshFailedTitle : copy.mapUnavailableTitle}
            description={
              mapQuery.data !== undefined
                ? copy.mapRefreshFailedDescription
                : undefined
            }
            detail={getQueryErrorDetail(mapQuery.error, copy.unexpectedError)}
            tone="error"
          />
        ) : null}

        {!mapQuery.isLoading && !mapQuery.isError && mapQuery.data === null ? (
          <StateNotice
            title={copy.mapUnavailableTitle}
            description={copy.mapDetailsUnavailableDescription}
          />
        ) : null}

        {mapQuery.data ? (
          <div className="page-stack">
            {!mapQuery.data.thumbnailUrl ? (
              <StateNotice
                title={copy.mapThumbnailUnavailableTitle}
                description={copy.mapThumbnailUnavailableDescription}
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
                  alt={mapQuery.data.mapName ?? copy.mapThumbnailAlt}
                  className="server-map-thumb__image"
                />
              </a>
            ) : null}

            <dl className="server-detail-meta">
              <div className="server-detail-meta__item">
                <dt>{copy.mapName}</dt>
                <dd>{mapQuery.data.mapName ?? copy.unavailable}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.seed}</dt>
                <dd>{mapQuery.data.seed ?? copy.unavailable}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.size}</dt>
                <dd>{mapQuery.data.size ?? copy.unavailable}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.fetched}</dt>
                <dd>{formatServerTimestamp(mapQuery.data.fetchedAtUtc, language)}</dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>RustMaps</dt>
                <dd>
                  {mapQuery.data.rustMapsUrl ? (
                    <a
                      href={mapQuery.data.rustMapsUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="map-action-link"
                    >
                      {copy.openRustMaps}
                    </a>
                  ) : (
                    copy.unavailable
                  )}
                </dd>
              </div>
              <div className="server-detail-meta__item">
                <dt>{copy.mapFile}</dt>
                <dd>
                  {mapQuery.data.mapFileUrl ? (
                    <a
                      href={mapQuery.data.mapFileUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="map-action-link"
                    >
                      {copy.downloadMapFile}
                    </a>
                  ) : (
                    copy.unavailable
                  )}
                </dd>
              </div>
            </dl>
            </div>
          </div>
        ) : null}
      </SectionCard>

      <SectionCard
        title={copy.playersTitle}
        description={copy.playersDescription}
      >
        {playersQuery.isLoading && playersQuery.data === undefined ? (
          <StateNotice
            title={copy.loadingPlayersTitle}
            description={copy.loadingPlayersDescription}
          />
        ) : null}

        {playersQuery.isFetching && playersQuery.data !== undefined ? (
          <StateNotice
            title={copy.refreshingPlayersTitle}
            description={copy.refreshingPlayersDescription}
          />
        ) : null}

        {playersQuery.isError ? (
          <StateNotice
            title={
              playersQuery.data !== undefined
                ? copy.playerRefreshFailedTitle
                : copy.playerRequestFailedTitle
            }
            description={
              playersQuery.data !== undefined
                ? copy.playerRefreshFailedDescription
                : undefined
            }
            detail={getQueryErrorDetail(playersQuery.error, copy.unexpectedError)}
            tone="error"
          />
        ) : null}

        {!playersQuery.isLoading && !playersQuery.isError && players.length === 0 ? (
          <StateNotice
            title={hasPlayerSnapshot ? copy.noPlayersTitle : copy.playersUnavailableTitle}
            description={
              hasPlayerSnapshot
                ? copy.noPlayersDescription
                : copy.playersUnavailableDescription
            }
          />
        ) : null}

        {!playersQuery.isLoading && !playersQuery.isError && players.length > 0 ? (
          <div className="page-stack">
            <div className="player-list">
              {players.map((player, index) => (
                <div key={`${player.playerName ?? 'player'}-${index}`} className="player-list__item">
                  <div className="player-list__identity">
                    <strong>{player.playerName ?? copy.unnamedPlayer}</strong>
                  </div>
                  <dl className="player-list__stats">
                    <div>
                      <dt>{copy.duration}</dt>
                      <dd>{formatPlayerDuration(player.durationSeconds, language)}</dd>
                    </div>
                  </dl>
                </div>
              ))}
            </div>
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}
