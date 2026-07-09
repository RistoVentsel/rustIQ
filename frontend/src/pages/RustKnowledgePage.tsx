import { Link } from 'react-router-dom'

import { SectionCard } from '../components/SectionCard'
import { knowledgeGuides } from '../features/knowledge/rustKnowledgeContent'

const featuredGuide = knowledgeGuides.find((guide) => guide.featured) ?? knowledgeGuides[0]
const regularGuides = knowledgeGuides.filter((guide) => guide.id !== featuredGuide.id)

export function RustKnowledgePage() {
  return (
    <div className="page-stack knowledge-page">
      <section className="knowledge-hero-panel">
        <div className="knowledge-hero-panel__copy">
          <p className="page-eyebrow">Rust Knowledge</p>
          <h2 className="page-heading">Field notes for better wipe decisions</h2>
          <p className="page-text">
            Visual RustIQ guides for server choice, progression routes, plant genetics, base systems, patch changes, and server ops.
          </p>
          <div className="knowledge-hero-panel__stats" aria-label="Knowledge guide stats">
            <span>{knowledgeGuides.length} guides</span>
            <span>Official sources</span>
            <span>Patch-aware notes</span>
          </div>
        </div>

        <div className="knowledge-hero-panel__art" aria-hidden="true">
          <img src="/rustiq-logo.png" alt="" />
          <div>
            <span>RustIQ</span>
            <strong>Knowledge Hub</strong>
          </div>
        </div>
      </section>

      <section className="knowledge-home-grid" aria-label="Rust knowledge guides">
        <Link
          to={`/rust-knowledge/${featuredGuide.id}`}
          className="knowledge-home-card knowledge-home-card--featured"
        >
          <GuideCardMedia guide={featuredGuide} />
          <div className="knowledge-home-card__content">
            <span className="knowledge-home-card__label">{featuredGuide.label}</span>
            <h3>{featuredGuide.title}</h3>
            <p>{featuredGuide.summary}</p>
            <div className="knowledge-home-card__meta">
              <span>{featuredGuide.readTime}</span>
              <span>{featuredGuide.keyTakeaways.length} key ideas</span>
            </div>
          </div>
        </Link>

        {regularGuides.map((guide) => (
          <Link
            key={guide.id}
            to={`/rust-knowledge/${guide.id}`}
            className="knowledge-home-card"
          >
            <GuideCardMedia guide={guide} />
            <div className="knowledge-home-card__content">
              <span className="knowledge-home-card__label">{guide.label}</span>
              <h3>{guide.title}</h3>
              <p>{guide.summary}</p>
              <div className="knowledge-home-card__meta">
                <span>{guide.readTime}</span>
                <span>{guide.sections.length || 1} sections</span>
              </div>
            </div>
          </Link>
        ))}
      </section>

      <SectionCard
        title="How To Use This"
        description="The index stays short. Each topic page carries its own examples, source links, and RustIQ use case."
      >
        <div className="knowledge-use-list">
          <article>
            <span>1</span>
            <div>
              <h3>Pick the decision you are making</h3>
              <p>Joining a server, planning progression, farming plants, choosing a route, or checking a patch change.</p>
            </div>
          </article>
          <article>
            <span>2</span>
            <div>
              <h3>Read the short guide page</h3>
              <p>Each guide has takeaways first, then the details. Long topics like farming get their own layout.</p>
            </div>
          </article>
          <article>
            <span>3</span>
            <div>
              <h3>Use sources when facts are patch-sensitive</h3>
              <p>Rust changes often, so every guide keeps official or high-signal source links close to the content.</p>
            </div>
          </article>
        </div>
      </SectionCard>
    </div>
  )
}

function GuideCardMedia({ guide }: { guide: typeof knowledgeGuides[number] }) {
  return (
    <div className="knowledge-home-card__media">
      <img src={guide.image} alt="" loading="lazy" />
      <div className="knowledge-home-card__media-overlay">
        <span>{guide.visualLabel}</span>
        <strong>{guide.visualStat}</strong>
      </div>
    </div>
  )
}
