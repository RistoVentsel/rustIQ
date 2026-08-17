declare global {
  interface Window {
    adsbygoogle: Array<Record<string, unknown>> & { pauseAdRequests?: 0 | 1 }
  }
}

export function pauseAds() {
  window.adsbygoogle = window.adsbygoogle || []
  window.adsbygoogle.pauseAdRequests = 1
}

export function resumeAds() {
  window.adsbygoogle = window.adsbygoogle || []
  window.adsbygoogle.pauseAdRequests = 0
}
