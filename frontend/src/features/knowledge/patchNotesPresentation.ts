import { getLanguageLocale, type Language } from '../../i18n/language'

export const latestPatchNotesQueryKey = ['patch-notes', 'latest'] as const

const patchNotesPresentationCopy: Record<Language, { dateUnavailable: string }> = {
  en: {
    dateUnavailable: 'Date unavailable',
  },
  ru: {
    dateUnavailable: 'Дата недоступна',
  },
}

export function formatPatchNoteDate(value: string, language: Language = 'en') {
  const parsed = Date.parse(value)

  if (Number.isNaN(parsed)) {
    return patchNotesPresentationCopy[language].dateUnavailable
  }

  return new Intl.DateTimeFormat(getLanguageLocale(language), {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(parsed)
}
