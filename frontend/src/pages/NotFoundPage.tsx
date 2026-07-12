import { Link } from 'react-router-dom'

import type { Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

const notFoundCopy: Record<Language, {
  eyebrow: string
  heading: string
  description: string
  back: string
}> = {
  en: {
    eyebrow: '404',
    heading: 'Page not found',
    description: 'The current route is not defined. Return to the server list to continue.',
    back: 'Back to servers',
  },
  ru: {
    eyebrow: '404',
    heading: 'Страница не найдена',
    description: 'Текущий маршрут не определен. Вернитесь к списку серверов, чтобы продолжить.',
    back: 'Вернуться к серверам',
  },
}

export function NotFoundPage() {
  const { language } = useLanguage()
  const copy = notFoundCopy[language]

  return (
    <section className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">{copy.eyebrow}</p>
        <h2 className="page-heading">{copy.heading}</h2>
        <p className="page-text">
          {copy.description}
        </p>
      </div>

      <div>
        <Link to="/" className="button-link">
          {copy.back}
        </Link>
      </div>
    </section>
  )
}
