import { useEffect, useState } from 'react'
import { Link, Outlet, useLocation } from 'react-router-dom'

import { languageOptions, type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'
import { useDocumentMeta } from '../seo/useDocumentMeta'

type Theme = 'dark' | 'light'
type NavItemId = 'servers' | 'minigames' | 'knowledge'

const navItems = [
  {
    id: 'servers',
    to: '/',
  },
  {
    id: 'minigames',
    to: '/minigames',
  },
  {
    id: 'knowledge',
    to: '/rust-knowledge',
  },
] satisfies Array<{ id: NavItemId, to: string }>

const appShellCopy: Record<Language, {
  appTitle: string
  nav: Record<NavItemId, { label: string, meta: string }>
  theme: {
    dark: string
    light: string
    switchTo: string
  }
  languageLabel: string
  sponsorInquiries: string
  privacyPolicy: string
  about: string
  contact: string
  disclaimer: string
}> = {
  en: {
    appTitle: 'Server Monitor',
    nav: {
      servers: { label: 'Servers', meta: 'Live browser' },
      minigames: { label: 'Minigames', meta: 'Daily challenges' },
      knowledge: { label: 'Rust Knowledge', meta: 'Guides & facts' },
    },
    theme: {
      dark: 'Dark',
      light: 'Light',
      switchTo: 'Switch to',
    },
    languageLabel: 'Language',
    sponsorInquiries: 'Sponsor inquiries',
    privacyPolicy: 'Privacy Policy',
    about: 'About',
    contact: 'Contact',
    disclaimer: 'rustIQ is an independent project and is not affiliated with or endorsed by Facepunch Studios. Rust is a trademark of Facepunch Studios.',
  },
  ru: {
    appTitle: 'Монитор серверов',
    nav: {
      servers: { label: 'Серверы', meta: 'Онлайн-браузер' },
      minigames: { label: 'Мини-игры', meta: 'Ежедневные задания' },
      knowledge: { label: 'База знаний', meta: 'Гайды и факты' },
    },
    theme: {
      dark: 'Темная',
      light: 'Светлая',
      switchTo: 'Переключить на',
    },
    languageLabel: 'Язык',
    sponsorInquiries: 'Вопросы спонсорства',
    privacyPolicy: 'Политика конфиденциальности',
    about: 'О проекте',
    contact: 'Контакты',
    disclaimer: 'rustIQ — независимый проект, не аффилированный с Facepunch Studios и не одобренный ими. Rust — торговая марка Facepunch Studios.',
  },
}

export function AppShell() {
  const location = useLocation()
  const { language, setLanguage } = useLanguage()
  const copy = appShellCopy[language]
  useDocumentMeta()
  const [theme, setTheme] = useState<Theme>(() => {
    const savedTheme = window.localStorage.getItem('rustiq-theme')
    return savedTheme === 'light' ? 'light' : 'dark'
  })

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    window.localStorage.setItem('rustiq-theme', theme)
  }, [theme])

  const nextTheme = theme === 'dark' ? 'light' : 'dark'
  const isNavItemActive = (to: string) => (
    to === '/'
      ? location.pathname === '/' || location.pathname.startsWith('/servers')
      : location.pathname === to || location.pathname.startsWith(`${to}/`)
  )
  const isServerRoute = location.pathname === '/' || location.pathname.startsWith('/servers')

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__inner">
          <div className="app-brand">
            <img className="app-brand__logo" src="/rustiq-logo.png" alt="rustIQ" />
            <div>
              <p className="app-eyebrow">rustIQ</p>
              <h1 className="app-title">{copy.appTitle}</h1>
            </div>
          </div>

          <nav aria-label="Primary">
            <ul className="app-nav">
              {navItems.map((item) => {
                const isActive = isNavItemActive(item.to)
                const navCopy = copy.nav[item.id]

                return (
                  <li key={item.to}>
                    <Link
                      to={item.to}
                      className={isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link'}
                      aria-current={isActive ? 'page' : undefined}
                    >
                      <span className="app-nav__label">{navCopy.label}</span>
                      <span className="app-nav__meta">{navCopy.meta}</span>
                    </Link>
                  </li>
                )
              })}
            </ul>
          </nav>

          <div className="app-controls">
            <div className="language-toggle" role="group" aria-label={copy.languageLabel}>
              {languageOptions.map((option) => (
                <button
                  key={option.value}
                  type="button"
                  className={language === option.value ? 'language-toggle__button language-toggle__button--active' : 'language-toggle__button'}
                  onClick={() => setLanguage(option.value)}
                  aria-pressed={language === option.value}
                  title={option.name}
                >
                  {option.label}
                </button>
              ))}
            </div>

            <button
              type="button"
              className="theme-toggle"
              onClick={() => setTheme(nextTheme)}
              aria-label={`${copy.theme.switchTo} ${nextTheme === 'dark' ? copy.theme.dark : copy.theme.light}`}
              aria-pressed={theme === 'light'}
            >
              <span className="theme-toggle__label">{copy.theme.dark}</span>
              <span className="theme-toggle__track" aria-hidden="true">
                <span className="theme-toggle__thumb" />
              </span>
              <span className="theme-toggle__label">{copy.theme.light}</span>
            </button>
          </div>
        </div>
      </header>

      <main className={isServerRoute ? 'app-main app-main--wide' : 'app-main'}>
        <Outlet />
      </main>

      <footer className="app-footer">
        <nav className="app-footer__links" aria-label="Footer">
          <Link to="/about">{copy.about}</Link>
          <Link to="/contact">{copy.contact}</Link>
          <Link to="/rust-knowledge">{copy.nav.knowledge.label}</Link>
          <Link to="/privacy">{copy.privacyPolicy}</Link>
        </nav>
        <p>
          {copy.sponsorInquiries}:{' '}
          <a href="mailto:greyezel@gmail.com">greyezel@gmail.com</a>
        </p>
        <p className="app-footer__disclaimer">{copy.disclaimer}</p>
      </footer>
    </div>
  )
}
