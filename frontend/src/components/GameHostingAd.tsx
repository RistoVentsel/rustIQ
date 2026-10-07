import { useEffect, useId, useRef, useState } from 'react'

import { type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

/**
 * rustIQ's affiliate link from the Game Hosting partner panel. Their terms ask
 * for rel="sponsored" on it and a commission disclosure next to it.
 */
export const gameHostingAffiliateUrl = 'https://gamehosting.app/r/grey/rust'

/*
 * File names, paths and class names here avoid the words ad blockers match on
 * ("ad", "banner", "sponsor", "partner", "affiliate" and IAB sizes like 728x90),
 * so the self-hosted banner is not caught by the generic filter rules.
 */
const imagePath = '/img/hosting/rust'

const gameHostingAdCopy: Record<Language, {
  label: string
  alt: string
  disclosure: string
}> = {
  en: {
    label: 'Sponsored',
    alt: 'Game Hosting: your own Rust server. Play with your clan anytime. Try it free for 24 hours, no card. 10% off your first payment.',
    disclosure: 'Affiliate link: we earn a commission if you sign up, at no extra cost to you.',
  },
  ru: {
    label: 'Реклама',
    alt: 'Game Hosting: свой сервер Rust. Играйте с кланом когда угодно. 24 часа бесплатно, без карты. Скидка 10% на первый платёж.',
    disclosure: 'Партнёрская ссылка: мы получаем комиссию, если вы зарегистрируетесь, без доплаты для вас.',
  },
}

type PictureProps = {
  theme: 'dark' | 'light'
  alt: string
  onLoad: () => void
  onError: () => void
}

function HostingPicture({ theme, alt, onLoad, onError }: PictureProps) {
  const src = (size: string, scale = '') => `${imagePath}-${size}-${theme}${scale}.png`

  return (
    <picture className={`hosting-card__picture hosting-card__picture--${theme}`}>
      <source
        media="(max-width: 760px)"
        width={320}
        height={100}
        srcSet={`${src('narrow')} 1x, ${src('narrow', '@2x')} 2x`}
      />
      <img
        className="hosting-card__image"
        src={src('wide')}
        srcSet={`${src('wide')} 1x, ${src('wide', '@2x')} 2x`}
        width={728}
        height={90}
        alt={alt}
        decoding="async"
        onLoad={onLoad}
        onError={onError}
      />
    </picture>
  )
}

/**
 * The Game Hosting Rust banner, placed above the server list as their media
 * kit recommends for a leaderboard. Both theme variants are rendered and the
 * stylesheet shows the one matching the site theme, so toggling the theme
 * swaps the banner without a re-render. Phones get the 320x100 cut.
 *
 * If a blocker still stops the image or hides the link, the whole card goes
 * away rather than leaving the label and disclosure on their own.
 */
export function GameHostingAd() {
  const { language } = useLanguage()
  const copy = gameHostingAdCopy[language]
  const labelId = useId()
  const linkRef = useRef<HTMLAnchorElement | null>(null)
  const [isBlocked, setIsBlocked] = useState(false)

  /*
   * Cosmetic filters (Brave Shields, uBlock) hide the link or the image with
   * display:none, often a moment after the image has loaded, so a one-off check
   * at load time misses them. The image's width/height attributes reserve its
   * height before it loads, so a link that collapses to zero height at any
   * point means something hid the banner.
   */
  useEffect(() => {
    const link = linkRef.current

    if (isBlocked || !link || typeof ResizeObserver === 'undefined') {
      return
    }

    const observer = new ResizeObserver(() => {
      if (link.offsetHeight === 0) {
        setIsBlocked(true)
      }
    })

    observer.observe(link)
    return () => observer.disconnect()
  }, [isBlocked])

  if (isBlocked) {
    return null
  }

  const hide = () => setIsBlocked(true)
  const hideIfLinkHidden = () => {
    const link = linkRef.current

    if (link && link.offsetHeight === 0) {
      hide()
    }
  }

  return (
    <aside className="hosting-card" aria-labelledby={labelId}>
      <span id={labelId} className="hosting-card__label">{copy.label}</span>
      <a
        ref={linkRef}
        className="hosting-card__link"
        href={gameHostingAffiliateUrl}
        target="_blank"
        rel="sponsored noopener"
      >
        <HostingPicture theme="dark" alt={copy.alt} onLoad={hideIfLinkHidden} onError={hide} />
        <HostingPicture theme="light" alt={copy.alt} onLoad={hideIfLinkHidden} onError={hide} />
      </a>
      <p className="hosting-card__disclosure">{copy.disclosure}</p>
    </aside>
  )
}
