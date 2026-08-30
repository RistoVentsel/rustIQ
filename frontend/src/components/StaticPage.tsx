import { ContentAd } from './ContentAd'
import type { StaticPageCopy } from '../content/sitePages'
import { useAdsGate } from '../hooks/useAdsGate'

/**
 * Shared renderer for the site's written pages (About, Contact, Privacy).
 * The same copy objects are consumed by scripts/prerender.mjs, so the static
 * HTML crawlers receive matches what readers see.
 */
export function StaticPage({ copy }: { copy: StaticPageCopy }) {
  useAdsGate(true)

  return (
    <section className="page-stack">
      <div className="page-copy">
        <p className="page-eyebrow">{copy.eyebrow}</p>
        <h2 className="page-heading">{copy.heading}</h2>
        {copy.lead ? <p className="page-lead">{copy.lead}</p> : null}
        <p className="page-text">{copy.intro}</p>
      </div>

      <ContentAd placement="article" />

      {copy.sections.map((section) => (
        <div className="page-copy" key={section.heading}>
          <h3 className="page-heading">{section.heading}</h3>
          {section.body.map((paragraph) => (
            <p className="page-text" key={paragraph.slice(0, 24)}>
              {paragraph}
            </p>
          ))}
        </div>
      ))}
    </section>
  )
}
