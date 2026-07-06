const rawApiBaseUrl = import.meta.env.VITE_API_BASE_URL

function normalizeBaseUrl(value: string) {
  return value.endsWith('/') ? value.slice(0, -1) : value
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
