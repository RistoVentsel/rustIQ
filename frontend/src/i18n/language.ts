export const languages = ['en', 'ru'] as const
export type Language = typeof languages[number]

export const defaultLanguage: Language = 'en'

export const languageOptions: Array<{ value: Language, label: string, name: string }> = [
  { value: 'en', label: 'EN', name: 'English' },
  { value: 'ru', label: 'RU', name: 'Русский' },
]

const languageLocales: Record<Language, string> = {
  en: 'en',
  ru: 'ru-RU',
}

const languageMeta: Record<Language, { title: string, description: string }> = {
  en: {
    title: 'rustIQ | Rust Server Monitor',
    description: 'rustIQ tracks Rust server populations, wipe timing, map details, rankings, and quick Rust knowledge guides.',
  },
  ru: {
    title: 'rustIQ | Монитор серверов Rust',
    description: 'rustIQ отслеживает онлайн серверов Rust, вайпы, карты, рейтинги и короткие гайды по Rust.',
  },
}

export function getLanguageLocale(language: Language) {
  return languageLocales[language]
}

export function getLanguageMeta(language: Language) {
  return languageMeta[language]
}

export function isLanguage(value: string | null): value is Language {
  return value === 'en' || value === 'ru'
}

export function formatRussianCount(value: number, one: string, few: string, many: string) {
  const absoluteValue = Math.abs(value)
  const lastTwoDigits = absoluteValue % 100
  const lastDigit = absoluteValue % 10

  if (lastTwoDigits >= 11 && lastTwoDigits <= 14) {
    return `${value} ${many}`
  }

  if (lastDigit === 1) {
    return `${value} ${one}`
  }

  if (lastDigit >= 2 && lastDigit <= 4) {
    return `${value} ${few}`
  }

  return `${value} ${many}`
}
