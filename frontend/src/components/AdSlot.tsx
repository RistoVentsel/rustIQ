import { useEffect, useRef } from 'react'
import { useLocation } from 'react-router-dom'

import { appConfig } from '../lib/config'

declare global {
  interface Window {
    adsbygoogle?: Array<Record<string, unknown>>
  }
}

type AdSlotProps = {
  slotId: string
  format?: string
  className?: string
  fullWidthResponsive?: boolean
}

export function AdSlot({ slotId, format = 'auto', className, fullWidthResponsive = true }: AdSlotProps) {
  const location = useLocation()
  const insRef = useRef<HTMLModElement | null>(null)

  useEffect(() => {
    if (!appConfig.adsenseClientId || !insRef.current) {
      return
    }

    try {
      (window.adsbygoogle = window.adsbygoogle ?? []).push({})
    } catch {
      // adsbygoogle script may not be loaded yet (blocked, ad-blocker, or still fetching).
    }
    // Re-request an ad whenever the route changes, since this is a single-page app
    // and a fresh <ins> element (via the location-based key below) needs its own request.
  }, [location.pathname])

  if (!appConfig.adsenseClientId) {
    return null
  }

  return (
    <ins
      key={`${slotId}-${location.pathname}`}
      ref={insRef}
      className={className ? `adsbygoogle ${className}` : 'adsbygoogle'}
      style={{ display: 'block' }}
      data-ad-client={appConfig.adsenseClientId}
      data-ad-slot={slotId}
      data-ad-format={format}
      data-full-width-responsive={fullWidthResponsive ? 'true' : 'false'}
    />
  )
}