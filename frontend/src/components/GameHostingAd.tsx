import { type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

/**
 * rustIQ's affiliate link from the Game Hosting partner panel. Their terms ask
 * for rel="sponsored" on it and a commission disclosure next to it.
 */
export const gameHostingAffiliateUrl = 'https://gamehosting.app/r/grey/rust'

const bannerPath = '/partners/game-hosting/game-hosting-rust'

const gameHostingAdCopy: Record<Language, {
  label: string
  alt: string
  offer: string
  disclosure: string
}> = {
  en: {
    label: 'Sponsored',
    alt: 'Game Hosting: your own Rust server. Play with your clan anytime. Try it free for 24 hours, no card. 10% off your first payment.',
    offer: 'Try it free for 24 hours, no card. 10% off your first payment through this link.',
    disclosure: 'Affiliate link: we earn a commission if you sign up, at no extra cost to you.',
  },
  ru: {
    label: 'Реклама',
    alt: 'Game Hosting: свой сервер Rust. Играйте с кланом когда угодно. 24 часа бесплатно, без карты. Скидка 10% на первый платёж.',
    offer: '24 часа бесплатно, без карты. Скидка 10% на первый платёж по этой ссылке.',
    disclosure: 'Партнёрская ссылка: мы получаем комиссию, если вы зарегистрируетесь, без доплаты для вас.',
  },
}

function BannerPicture({ theme, alt }: { theme: 'dark' | 'light', alt: string }) {
  const src = (size: string, scale = '') => `${bannerPath}-${size}-${theme}${scale}.png`

  return (
    <picture className={`game-hosting-ad__picture game-hosting-ad__picture--${theme}`}>
      <source
        media="(max-width: 760px)"
        width={320}
        height={100}
        srcSet={`${src('320x100')} 1x, ${src('320x100', '@2x')} 2x`}
      />
      <img
        className="game-hosting-ad__image"
        src={src('728x90')}
        srcSet={`${src('728x90')} 1x, ${src('728x90', '@2x')} 2x`}
        width={728}
        height={90}
        alt={alt}
        loading="lazy"
        decoding="async"
      />
    </picture>
  )
}

/**
 * The Game Hosting Rust banner, placed above the server list as their media
 * kit recommends for a leaderboard. Both theme variants are rendered and the
 * stylesheet shows the one matching the site theme, so toggling the theme
 * swaps the banner without a re-render. Phones get the 320x100 cut.
 */
export function GameHostingAd() {
  const { language } = useLanguage()
  const copy = gameHostingAdCopy[language]

  return (
    <aside className="game-hosting-ad" aria-label={copy.label}>
      <span className="content-ad__label">{copy.label}</span>
      <a
        className="game-hosting-ad__link"
        href={gameHostingAffiliateUrl}
        target="_blank"
        rel="sponsored noopener"
      >
        <BannerPicture theme="dark" alt={copy.alt} />
        <BannerPicture theme="light" alt={copy.alt} />
      </a>
      <p className="game-hosting-ad__offer">{copy.offer}</p>
      <p className="game-hosting-ad__disclosure">{copy.disclosure}</p>
    </aside>
  )
}
