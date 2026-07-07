import { appConfig } from '../lib'

export class ApiError extends Error {
  readonly status: number
  readonly url: string

  constructor(message: string, status: number, url: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.url = url
  }
}

type RequestOptions = {
  query?: Record<string, string | number | boolean | null | undefined>
  signal?: AbortSignal
}

function buildUrl(path: string, query?: RequestOptions['query']) {
  const url = new URL(path, `${appConfig.apiBaseUrl}/`)

  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') {
        continue
      }

      url.searchParams.set(key, String(value))
    }
  }

  return url.toString()
}

async function readErrorMessage(response: Response) {
  const contentType = response.headers.get('content-type') ?? ''

  if (contentType.includes('application/json')) {
    const payload = (await response.json()) as { detail?: string; title?: string }
    return payload.detail ?? payload.title ?? `${response.status} ${response.statusText}`
  }

  const text = await response.text()
  return text || `${response.status} ${response.statusText}`
}

export async function getJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const url = buildUrl(path, options.query)
  const response = await fetch(url, {
    method: 'GET',
    signal: options.signal,
    cache: 'no-store',
    headers: {
      Accept: 'application/json',
    },
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response), response.status, url)
  }

  return (await response.json()) as T
}

export async function postJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const url = buildUrl(path, options.query)
  const response = await fetch(url, {
    method: 'POST',
    signal: options.signal,
    cache: 'no-store',
    headers: {
      Accept: 'application/json',
    },
  })

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response), response.status, url)
  }

  return (await response.json()) as T
}
