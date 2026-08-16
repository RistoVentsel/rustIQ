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

export const appConfig = {
  apiBaseUrl: readApiBaseUrl(),
  get apiHubUrl() {
    return `${this.apiBaseUrl}/hubs/servers`
  },
  adsenseClientId: readAdsenseClientId(),
} as const
