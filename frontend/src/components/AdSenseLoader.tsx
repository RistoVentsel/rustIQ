import { useEffect } from 'react'

import { appConfig } from '../lib/config'

const scriptId = 'adsbygoogle-loader'

export function AdSenseLoader() {
  useEffect(() => {
    const clientId = appConfig.adsenseClientId

    if (!clientId || document.getElementById(scriptId)) {
      return
    }

    const script = document.createElement('script')
    script.id = scriptId
    script.async = true
    script.src = `https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${clientId}`
    script.crossOrigin = 'anonymous'
    document.head.appendChild(script)
  }, [])

  return null
}