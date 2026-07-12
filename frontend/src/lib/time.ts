import { getLanguageLocale, type Language } from '../i18n/language'

const timeCopy: Record<Language, {
  unavailable: string
  now: string
  yesterday: string
  overAWeekAgo: string
}> = {
  en: {
    unavailable: 'Unavailable',
    now: 'Now',
    yesterday: 'Yesterday',
    overAWeekAgo: 'Over a week ago',
  },
  ru: {
    unavailable: 'Недоступно',
    now: 'Сейчас',
    yesterday: 'Вчера',
    overAWeekAgo: 'Больше недели назад',
  },
}

export function getSecondsAgo(timestamp: string, nowMs: number) {
  const parsed = Date.parse(timestamp)

  if (Number.isNaN(parsed)) {
    return null
  }

  return Math.max(0, Math.floor((nowMs - parsed) / 1000))
}

export function formatSecondsAgo(seconds: number | null | undefined, language: Language = 'en') {
  if (seconds === null || seconds === undefined) {
    return timeCopy[language].unavailable
  }

  return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'always' }).format(-seconds, 'second')
}

export function formatCoarseRelativeTime(seconds: number | null | undefined, language: Language = 'en') {
  if (seconds === null || seconds === undefined) {
    return timeCopy[language].unavailable
  }

  if (seconds < 60) {
    return timeCopy[language].now
  }

  if (seconds < 60 * 60) {
    return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'always' }).format(
      -Math.floor(seconds / 60),
      'minute',
    )
  }

  if (seconds < 2 * 60 * 60) {
    return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'always' }).format(-1, 'hour')
  }

  if (seconds < 24 * 60 * 60) {
    return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'always' }).format(
      -Math.floor(seconds / (60 * 60)),
      'hour',
    )
  }

  if (seconds < 2 * 24 * 60 * 60) {
    return timeCopy[language].yesterday
  }

  if (seconds < 7 * 24 * 60 * 60) {
    return new Intl.RelativeTimeFormat(getLanguageLocale(language), { numeric: 'always' }).format(
      -Math.floor(seconds / (24 * 60 * 60)),
      'day',
    )
  }

  return timeCopy[language].overAWeekAgo
}
