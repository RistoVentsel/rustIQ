import { useEffect, useState } from 'react'

import { type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

/**
 * The survey behind the rail. rustIQ is a final thesis project and the form is
 * how its research data is collected, so the link also sits in the footer to stay
 * reachable after somebody dismisses the rail.
 */
export const feedbackFormUrl = 'https://forms.gle/iZAZQQhEWTft6mfX9'

const dismissedStorageKey = 'rustiq-feedback-dismissed'

const feedbackAdCopy: Record<Language, {
  eyebrow: string
  title: string
  body: string
  call: string
  note: string
  dismiss: string
  dismissLabel: string
}> = {
  en: {
    eyebrow: '2-min survey',
    title: 'Decide what rustIQ builds next',
    body: 'rustIQ is my final thesis project and this survey is the research behind it. Tell me how you pick a server and it shapes what ships next.',
    call: 'Take the survey',
    note: 'Free · No sign-up · Opens in a new tab',
    dismiss: 'Not now',
    dismissLabel: 'Hide the survey',
  },
  ru: {
    eyebrow: 'Опрос · 2 мин',
    title: 'Решите, что появится в rustIQ',
    body: 'rustIQ — мой дипломный проект, а этот опрос и есть его исследование. Расскажите, как вы выбираете сервер, и это определит, что появится дальше.',
    call: 'Пройти опрос',
    note: 'Бесплатно · Без регистрации · В новой вкладке',
    dismiss: 'Не сейчас',
    dismissLabel: 'Скрыть опрос',
  },
}

function readDismissed() {
  try {
    return window.localStorage.getItem(dismissedStorageKey) === 'true'
  } catch {
    // Private browsing and blocked site data both throw here. Showing the rail
    // is the safe fallback: it stays dismissable for the rest of the visit.
    return false
  }
}

/**
 * A site-wide invitation to the feedback survey, pinned to the side of the
 * viewport so it rides along with the page as a visitor scrolls.
 *
 * It sits outside <main> in the shell, which keeps it clear of every page's own
 * layout - the long server list and the server detail page included - and lets
 * it stay in view on all of them. Narrower viewports have no room beside the
 * 1120px content column, so there it drops to the bottom corner instead.
 *
 * Dismissing it is remembered, so a visitor who says no is not asked again on
 * the next page or the next visit.
 */
export function FeedbackBanner() {
  const { language } = useLanguage()
  const copy = feedbackAdCopy[language]
  const [isDismissed, setIsDismissed] = useState(readDismissed)

  useEffect(() => {
    if (!isDismissed) {
      return
    }

    try {
      window.localStorage.setItem(dismissedStorageKey, 'true')
    } catch {
      // Nothing to do: the rail stays hidden for this visit either way.
    }
  }, [isDismissed])

  if (isDismissed) {
    return null
  }

  return (
    <aside className="feedback-ad" aria-label={copy.title}>
      <button
        type="button"
        className="feedback-ad__close"
        onClick={() => setIsDismissed(true)}
        aria-label={copy.dismissLabel}
      >
        <span aria-hidden="true">×</span>
      </button>

      <p className="feedback-ad__eyebrow">
        <span className="feedback-ad__pulse" aria-hidden="true" />
        {copy.eyebrow}
      </p>
      <p className="feedback-ad__title">{copy.title}</p>
      <p className="feedback-ad__body">{copy.body}</p>

      <a
        className="feedback-ad__call"
        href={feedbackFormUrl}
        target="_blank"
        rel="noreferrer"
      >
        {copy.call}
        <span className="feedback-ad__call-arrow" aria-hidden="true">→</span>
      </a>

      <p className="feedback-ad__note">{copy.note}</p>

      <button
        type="button"
        className="feedback-ad__dismiss"
        onClick={() => setIsDismissed(true)}
      >
        {copy.dismiss}
      </button>
    </aside>
  )
}
