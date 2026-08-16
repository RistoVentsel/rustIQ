import type { PropsWithChildren } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'

import { AdSenseLoader } from '../components/AdSenseLoader'
import { LanguageProvider } from '../i18n/LanguageContext'
import { queryClient } from './queryClient'

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <LanguageProvider>
      <QueryClientProvider client={queryClient}>
        <AdSenseLoader />
        {children}
      </QueryClientProvider>
    </LanguageProvider>
  )
}
