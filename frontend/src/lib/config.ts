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

export const appConfig = {
  apiBaseUrl: readApiBaseUrl(),
  get apiHubUrl() {
    return `${this.apiBaseUrl}/hubs/servers`
  },
} as const
