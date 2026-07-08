import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'

type Theme = 'dark' | 'light'

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
              <li>
                <NavLink
                  to="/"
                  className={({ isActive }) =>
                    isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link'
                  }
                  end
                >
                  Servers
                </NavLink>
              </li>
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

      <main className={location.pathname === '/' ? 'app-main app-main--wide' : 'app-main'}>
        <Outlet />
      </main>
    </div>
  )
}
