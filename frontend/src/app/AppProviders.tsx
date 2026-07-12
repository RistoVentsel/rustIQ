import type { PropsWithChildren } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'

import { LanguageProvider } from '../i18n/LanguageContext'
import { queryClient } from './queryClient'

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <LanguageProvider>
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    </LanguageProvider>
  )
}
