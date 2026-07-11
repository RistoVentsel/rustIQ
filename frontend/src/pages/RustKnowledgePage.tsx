import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'

import { getLatestPatchNotes, type PatchNote } from '../api'
import { SectionCard } from '../components/SectionCard'
import { formatPatchNoteDate, latestPatchNotesQueryKey } from '../features/knowledge/patchNotesPresentation'
import { knowledgeGuides, patchNotesGuideId } from '../features/knowledge/rustKnowledgeContent'

const featuredGuide = knowledgeGuides.find((guide) => guide.featured) ?? knowledgeGuides[0]
const regularGuides = knowledgeGuides.filter((guide) => guide.id !== featuredGuide.id)

export function RustKnowledgePage() {
  const patchNotesQuery = useQuery({
    queryKey: latestPatchNotesQueryKey,
    queryFn: ({ signal }) => getLatestPatchNotes(signal),
    staleTime: 15 * 60 * 1000,
  })
  const patchNotes = patchNotesQuery.data ?? []

  return (
    <div className="page-stack knowledge-page">
      <section className="knowledge-hero-panel">
        <div className="knowledge-hero-panel__copy">
          <p className="page-eyebrow">Rust Knowledge</p>
          <h2 className="page-heading">Field notes for better wipe decisions</h2>
          <p className="page-text">
            Visual RustIQ guides for tips and tricks, progression routes, plant genetics, base systems, keybinds, and the latest patch notes.
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
          <GuideCardContent
            guide={featuredGuide}
            isPatchNotesLoading={patchNotesQuery.isLoading}
            patchNotes={patchNotes}
          />
        </Link>

        {regularGuides.map((guide) => (
          <Link
            key={guide.id}
            to={`/rust-knowledge/${guide.id}`}
            className="knowledge-home-card"
          >
            <GuideCardMedia guide={guide} />
            <GuideCardContent
              guide={guide}
              isPatchNotesLoading={patchNotesQuery.isLoading}
              patchNotes={patchNotes}
            />
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
              <p>Learning quick survival habits, planning progression, farming plants, setting keybinds, checking base systems, or reading the latest patch notes.</p>
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

function GuideCardContent({
  guide,
  isPatchNotesLoading,
  patchNotes,
}: {
  guide: typeof knowledgeGuides[number]
  isPatchNotesLoading: boolean
  patchNotes: PatchNote[]
}) {
  const isPatchNotesGuide = guide.id === patchNotesGuideId

  return (
    <div className="knowledge-home-card__content">
      <span className="knowledge-home-card__label">{guide.label}</span>
      <h3>{guide.title}</h3>
      {isPatchNotesGuide ? (
        <PatchNoteCardPreview
          isLoading={isPatchNotesLoading}
          patchNotes={patchNotes}
          summary={guide.summary}
        />
      ) : (
        <p>{guide.summary}</p>
      )}
      <div className="knowledge-home-card__meta">
        <span>{guide.readTime}</span>
        <span>{isPatchNotesGuide ? 'Official feed' : `${guide.sections.length || 1} sections`}</span>
      </div>
    </div>
  )
}

function PatchNoteCardPreview({
  isLoading,
  patchNotes,
  summary,
}: {
  isLoading: boolean
  patchNotes: PatchNote[]
  summary: string
}) {
  if (isLoading) {
    return <p>Loading latest patch notes.</p>
  }

  if (patchNotes.length === 0) {
    return <p>{summary}</p>
  }

  return (
    <ol className="knowledge-patch-preview" aria-label="Latest Rust patch notes">
      {patchNotes.slice(0, 3).map((patchNote) => (
        <li key={patchNote.url}>
          <span>{formatPatchNoteDate(patchNote.publishedAtUtc)}</span>
          <strong>{patchNote.title}</strong>
        </li>
      ))}
    </ol>
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
