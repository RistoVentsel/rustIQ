import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <section className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">404</p>
        <h2 className="page-heading">Page not found</h2>
        <p className="page-text">
          The current route is not defined. Return to the server list to continue.
        </p>
      </div>

      <div>
        <Link to="/" className="button-link">
          Back to servers
        </Link>
      </div>
    </section>
  )
}
