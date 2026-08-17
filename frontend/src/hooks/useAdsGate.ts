import { useEffect } from 'react'

import { pauseAds, resumeAds } from '../lib/adsGate'

/**
 * Resumes Google Auto ads only while `hasContent` is true. Ads stay paused
 * by default (see index.html) and on unmount/navigation, so screens without
 * real publisher content never serve ads.
 */
export function useAdsGate(hasContent: boolean) {
  useEffect(() => {
    if (hasContent) {
      resumeAds()
    } else {
      pauseAds()
    }

    return () => {
      pauseAds()
    }
  }, [hasContent])
}
