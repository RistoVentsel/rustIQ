const rawApiBaseUrl = import.meta.env.VITE_API_BASE_URL

function normalizeBaseUrl(value: string) {
  const normalizedValue = value.length > 1 && value.endsWith('/')
    ? value.slice(0, -1)
    : value

  if (normalizedValue.startsWith('/')) {
    return `${window.location.origin}${normalizedValue}`
  }

  return normalizedValue
}

function readApiBaseUrl() {
  const value = rawApiBaseUrl?.trim()

  if (!value) {
    throw new Error('Missing VITE_API_BASE_URL. Set it in the frontend environment.')
  }

  return normalizeBaseUrl(value)
}

const defaultAdsenseClientId = 'ca-pub-1494964558491486'

function readAdsenseClientId() {
  const value = import.meta.env.VITE_ADSENSE_CLIENT_ID?.trim()
  return value ? value : defaultAdsenseClientId
}

/**
 * Manual ad unit slot IDs, created in the AdSense dashboard under
 * Ads > By ad unit. Each is optional: when a slot is not configured the
 * corresponding <ContentAd> renders nothing and the page relies on Auto ads.
 */
function readAdSlots() {
  const read = (value: string | undefined) => {
    const trimmed = value?.trim()
    return trimmed ? trimmed : null
  }

  return {
    servers: read(import.meta.env.VITE_ADSENSE_SLOT_SERVERS),
    knowledge: read(import.meta.env.VITE_ADSENSE_SLOT_KNOWLEDGE),
    minigames: read(import.meta.env.VITE_ADSENSE_SLOT_MINIGAMES),
    article: read(import.meta.env.VITE_ADSENSE_SLOT_ARTICLE),
  } as const
}

export const appConfig = {
  apiBaseUrl: readApiBaseUrl(),
  get apiHubUrl() {
    return `${this.apiBaseUrl}/hubs/servers`
  },
  adsenseClientId: readAdsenseClientId(),
  adSlots: readAdSlots(),
} as const

export type AdPlacement = keyof ReturnType<typeof readAdSlots>
