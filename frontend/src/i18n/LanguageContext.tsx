import { type PropsWithChildren, useEffect, useMemo, useState } from 'react'

import {
  defaultLanguage,
  getLanguageLocale,
  getLanguageMeta,
  isLanguage,
  type Language,
} from './language'
import { LanguageContext, type LanguageContextValue } from './languageState'

const languageStorageKey = 'rustiq-language'

function getInitialLanguage(): Language {
  const savedLanguage = window.localStorage.getItem(languageStorageKey)

  if (isLanguage(savedLanguage)) {
    return savedLanguage
  }

  const browserLanguage = window.navigator.language.toLowerCase()
  return browserLanguage.startsWith('ru') ? 'ru' : defaultLanguage
}

export function LanguageProvider({ children }: PropsWithChildren) {
  const [language, setLanguage] = useState<Language>(getInitialLanguage)
  const locale = getLanguageLocale(language)

  useEffect(() => {
    const meta = getLanguageMeta(language)
    const descriptionMeta = document.querySelector<HTMLMetaElement>('meta[name="description"]')
    const ogTitleMeta = document.querySelector<HTMLMetaElement>('meta[property="og:title"]')
    const ogDescriptionMeta = document.querySelector<HTMLMetaElement>('meta[property="og:description"]')

    document.documentElement.lang = locale
    document.title = meta.title
    window.localStorage.setItem(languageStorageKey, language)

    descriptionMeta?.setAttribute('content', meta.description)
    ogTitleMeta?.setAttribute('content', meta.title)
    ogDescriptionMeta?.setAttribute('content', meta.description)
  }, [language, locale])

  const value = useMemo<LanguageContextValue>(() => ({
    language,
    setLanguage,
    locale,
  }), [language, locale])

  return (
    <LanguageContext.Provider value={value}>
      {children}
    </LanguageContext.Provider>
  )
}
