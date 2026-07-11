export const latestPatchNotesQueryKey = ['patch-notes', 'latest'] as const

export function formatPatchNoteDate(value: string) {
  const parsed = Date.parse(value)

  if (Number.isNaN(parsed)) {
    return 'Date unavailable'
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(parsed)
}
