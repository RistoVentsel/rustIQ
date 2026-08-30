import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

import { useLanguage } from '../i18n/useLanguage'
import { findRouteMeta, siteUrl } from './siteMeta'

function setMeta(selector: string, attribute: 'name' | 'property', key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(selector)

  if (!element) {
    element = document.createElement('meta')
    element.setAttribute(attribute, key)
    document.head.appendChild(element)
  }

  element.setAttribute('content', content)
}

function setCanonical(href: string) {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]')

  if (!element) {
    element = document.createElement('link')
    element.rel = 'canonical'
    document.head.appendChild(element)
  }

  element.href = href
}

function setRobots(noindex: boolean) {
  const element = document.head.querySelector<HTMLMetaElement>('meta[name="robots"]')

  if (!noindex) {
    element?.remove()
    return
  }

  setMeta('meta[name="robots"]', 'name', 'robots', 'noindex, follow')
}

/**
 * Keeps title, description, canonical and robots in sync with the current
 * route once React takes over from the prerendered HTML.
 *
 * Without this, the SPA reset every page back to the site-wide title and a
 * canonical pointing at the homepage, which told Google every URL was a
 * duplicate of `/`.
 */
export function useDocumentMeta() {
  const { pathname } = useLocation()
  const { language, locale } = useLanguage()

  useEffect(() => {
    const route = findRouteMeta(pathname, language)
    // Server detail pages are generated per server and are intentionally kept
    // out of the index; see scripts/prerender.mjs.
    const isServerDetail = pathname.startsWith('/servers/')

    const title = route?.title ?? (isServerDetail
      ? 'Rust Server Details | rustIQ'
      : 'rustIQ | Rust Server Monitor')
    const description = route?.description ?? (isServerDetail
      ? 'Live population, map, wipe schedule and uptime history for a single Rust server.'
      : 'rustIQ tracks Rust server populations, wipe timing, map details, rankings, and quick Rust knowledge guides.')

    const canonicalPath = route?.path ?? (isServerDetail ? pathname : '/')
    const canonical = `${siteUrl}${canonicalPath}`

    document.documentElement.lang = locale
    document.title = title
    setMeta('meta[name="description"]', 'name', 'description', description)
    setCanonical(canonical)
    setMeta('meta[property="og:url"]', 'property', 'og:url', canonical)
    setMeta('meta[property="og:title"]', 'property', 'og:title', title)
    setMeta('meta[property="og:description"]', 'property', 'og:description', description)
    setRobots(Boolean(route?.noindex) || isServerDetail)
  }, [pathname, language, locale])
}
