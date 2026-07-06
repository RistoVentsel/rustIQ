export function getSecondsAgo(timestamp: string, nowMs: number) {
  const parsed = Date.parse(timestamp)

  if (Number.isNaN(parsed)) {
    return null
  }

  return Math.max(0, Math.floor((nowMs - parsed) / 1000))
}

export function formatSecondsAgo(seconds: number | null | undefined) {
  if (seconds === null || seconds === undefined) {
    return 'Unavailable'
  }

  return `${seconds}s ago`
}
