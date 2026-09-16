import { useEffect, useState } from 'react'

import { type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

/**
 * The survey behind the banner. rustIQ is a final thesis project and the form is
 * how its research data is collected, so the link also sits in the footer to stay
 * reachable after somebody dismisses the banner.
 */
export const feedbackFormUrl = 'https://forms.gle/iZAZQQhEWTft6mfX9'

const dismissedStorageKey = 'rustiq-feedback-dismissed'

const feedbackBannerCopy: Record<Language, {
  eyebrow: string
  title: string
  body: string
  call: string
  dismiss: string
  dismissLabel: string
}> = {
  en: {
    eyebrow: 'Help out',
    title: 'What would make rustIQ better?',
    body: 'rustIQ is my final thesis project, and the survey behind this is the research. A few minutes of your answers on how you find servers shapes what gets built next.',
    call: 'Take the survey',
    dismiss: 'Not now',
    dismissLabel: 'Hide the survey banner',
  },
  ru: {
    eyebrow: 'Помогите проекту',
    title: 'Что сделать в rustIQ лучше?',
    body: 'rustIQ — мой дипломный проект, а этот опрос и есть его исследование. Несколько минут ваших ответов о том, как вы ищете серверы, определят, что появится дальше.',
    call: 'Пройти опрос',
    dismiss: 'Не сейчас',
    dismissLabel: 'Скрыть баннер опроса',
  },
}

function readDismissed() {
  try {
    return window.localStorage.getItem(dismissedStorageKey) === 'true'
  } catch {
    // Private browsing and blocked site data both throw here. Showing the banner
    // is the safe fallback: it stays dismissable for the rest of the visit.
    return false
  }
}

/**
 * A site-wide invitation to the feedback survey, rendered above every page.
 *
 * Dismissing it is remembered, so a visitor who says no is not asked again on
 * the next page or the next visit.
 */
export function FeedbackBanner() {
  const { language } = useLanguage()
  const copy = feedbackBannerCopy[language]
  const [isDismissed, setIsDismissed] = useState(readDismissed)

  useEffect(() => {
    if (!isDismissed) {
      return
    }

    try {
      window.localStorage.setItem(dismissedStorageKey, 'true')
    } catch {
      // Nothing to do: the banner stays hidden for this visit either way.
    }
  }, [isDismissed])

  if (isDismissed) {
    return null
  }

  return (
    <aside className="feedback-banner" aria-label={copy.title}>
      <div className="feedback-banner__text">
        <p className="feedback-banner__eyebrow">{copy.eyebrow}</p>
        <p className="feedback-banner__title">{copy.title}</p>
        <p className="feedback-banner__body">{copy.body}</p>
      </div>

      <div className="feedback-banner__actions">
        <a
          className="feedback-banner__call"
          href={feedbackFormUrl}
          target="_blank"
          rel="noreferrer"
        >
          {copy.call}
        </a>
        <button
          type="button"
          className="feedback-banner__dismiss"
          onClick={() => setIsDismissed(true)}
          aria-label={copy.dismissLabel}
        >
          {copy.dismiss}
        </button>
      </div>
    </aside>
  )
}
