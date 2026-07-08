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

export function formatCoarseRelativeTime(seconds: number | null | undefined) {
  if (seconds === null || seconds === undefined) {
    return 'Unavailable'
  }

  if (seconds < 60) {
    return 'Now'
  }

  if (seconds < 60 * 60) {
    return `${Math.floor(seconds / 60)}m ago`
  }

  if (seconds < 2 * 60 * 60) {
    return '1h ago'
  }

  if (seconds < 24 * 60 * 60) {
    return `${Math.floor(seconds / (60 * 60))}h ago`
  }

  if (seconds < 2 * 24 * 60 * 60) {
    return 'Yesterday'
  }

  if (seconds < 7 * 24 * 60 * 60) {
    return `${Math.floor(seconds / (24 * 60 * 60))}d ago`
  }

  return 'Over a week ago'
}
