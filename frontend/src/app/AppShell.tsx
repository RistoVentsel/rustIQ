import { useEffect, useState } from 'react'
import { Link, Outlet, useLocation } from 'react-router-dom'

type Theme = 'dark' | 'light'

const navItems = [
  {
    to: '/',
    label: 'Servers',
    meta: 'Live browser',
  },
  {
    to: '/minigames',
    label: 'Minigames',
    meta: 'Daily challenges',
  },
  {
    to: '/rust-knowledge',
    label: 'Rust Knowledge',
    meta: 'Guides & facts',
  },
] as const

export function AppShell() {
  const location = useLocation()
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
              <h1 className="app-title">Server Monitor</h1>
            </div>
          </div>

          <nav aria-label="Primary">
            <ul className="app-nav">
              {navItems.map((item) => {
                const isActive = isNavItemActive(item.to)

                return (
                  <li key={item.to}>
                    <Link
                      to={item.to}
                      className={isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link'}
                      aria-current={isActive ? 'page' : undefined}
                    >
                      <span className="app-nav__label">{item.label}</span>
                      <span className="app-nav__meta">{item.meta}</span>
                    </Link>
                  </li>
                )
              })}
            </ul>
          </nav>

          <button
            type="button"
            className="theme-toggle"
            onClick={() => setTheme(nextTheme)}
            aria-label={`Switch to ${nextTheme} theme`}
            aria-pressed={theme === 'light'}
          >
            <span className="theme-toggle__label">Dark</span>
            <span className="theme-toggle__track" aria-hidden="true">
              <span className="theme-toggle__thumb" />
            </span>
            <span className="theme-toggle__label">Light</span>
          </button>
        </div>
      </header>

      <main className={isServerRoute ? 'app-main app-main--wide' : 'app-main'}>
        <Outlet />
      </main>

      <footer className="app-footer">
        <p>
          Sponsor inquiries:{' '}
          <a href="mailto:greyezel@gmail.com">greyezel@gmail.com</a>
        </p>
      </footer>
    </div>
  )
}
