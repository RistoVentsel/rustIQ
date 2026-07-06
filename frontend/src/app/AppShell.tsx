import { NavLink, Outlet } from 'react-router-dom'

export function AppShell() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="app-header__inner">
          <div>
            <p className="app-eyebrow">RustIQ</p>
            <h1 className="app-title">Server Monitor</h1>
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
        </div>
      </header>

      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
