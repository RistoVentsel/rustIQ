import { type PropsWithChildren, useEffect, useMemo, useState } from 'react'

import {
  defaultLanguage,
  getLanguageLocale,
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

  // Title, description and canonical are owned by useDocumentMeta(), which
  // knows the current route. This effect only persists the choice.
  useEffect(() => {
    window.localStorage.setItem(languageStorageKey, language)
  }, [language])

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
