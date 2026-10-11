import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'

import { getLatestPatchNotes, type PatchNote } from '../api'
import { SectionCard } from '../components/SectionCard'
import { formatPatchNoteDate, latestPatchNotesQueryKey } from '../features/knowledge/patchNotesPresentation'
import { getKnowledgeGuides, patchNotesGuideId, type KnowledgeGuide } from '../features/knowledge/rustKnowledgeContent'
import { ContentAd } from '../components/ContentAd'
import { useAdsGate } from '../hooks/useAdsGate'
import { formatRussianCount, type Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

const knowledgePageCopy: Record<Language, {
  eyebrow: string
  heading: string
  intro: string
  statsLabel: string
  guides: (count: number) => string
  officialSources: string
  patchAwareNotes: string
  hubTitle: string
  guideGridLabel: string
  howToTitle: string
  howToDescription: string
  steps: Array<{ title: string, description: string }>
  officialFeed: string
  sections: (count: number) => string
  loadingPatchNotes: string
  patchPreviewLabel: string
}> = {
  en: {
    eyebrow: 'Rust Knowledge',
    heading: 'Field notes for better wipe decisions',
    intro: 'Visual RustIQ guides for tips and tricks, progression routes, plant genetics, base systems, keybinds, and the latest patch notes.',
    statsLabel: 'Knowledge guide stats',
    guides: (count) => `${count} guides`,
    officialSources: 'Official sources',
    patchAwareNotes: 'Patch-aware notes',
    hubTitle: 'Knowledge Hub',
    guideGridLabel: 'Rust knowledge guides',
    howToTitle: 'How To Use This',
    howToDescription: 'The index stays short. Each topic page carries its own examples, source links, and RustIQ use case.',
    steps: [
      {
        title: 'Pick the decision you are making',
        description: 'Learning quick survival habits, planning progression, farming plants, setting keybinds, checking base systems, or reading the latest patch notes.',
      },
      {
        title: 'Read the short guide page',
        description: 'Each guide has takeaways first, then the details. Long topics like farming get their own layout.',
      },
      {
        title: 'Use sources when facts are patch-sensitive',
        description: 'Rust changes often, so every guide keeps official or high-signal source links close to the content.',
      },
    ],
    officialFeed: 'Official feed',
    sections: (count) => `${count} sections`,
    loadingPatchNotes: 'Loading latest patch notes.',
    patchPreviewLabel: 'Latest Rust patch notes',
  },
  ru: {
    eyebrow: 'База знаний Rust',
    heading: 'Полевые заметки для лучших решений на вайпе',
    intro: 'Визуальные гайды RustIQ по советам, прогрессу, генетике растений, системам базы, биндам и последним патчноутам.',
    statsLabel: 'Статистика гайдов',
    guides: (count) => formatRussianCount(count, 'гайд', 'гайда', 'гайдов'),
    officialSources: 'Официальные источники',
    patchAwareNotes: 'Заметки с учетом патчей',
    hubTitle: 'База знаний',
    guideGridLabel: 'Гайды по Rust',
    howToTitle: 'Как этим пользоваться',
    howToDescription: 'Индекс остается коротким. У каждой темы есть свои примеры, ссылки на источники и сценарий использования RustIQ.',
    steps: [
      {
        title: 'Выберите решение, которое принимаете',
        description: 'Быстрые привычки выживания, план прогресса, фермерство, бинды, системы базы или чтение последних патчноутов.',
      },
      {
        title: 'Прочитайте короткую страницу гайда',
        description: 'В каждом гайде сначала идут выводы, потом детали. Большие темы вроде фермы получают отдельную структуру страницы.',
      },
      {
        title: 'Используйте источники для фактов, зависящих от патчей',
        description: 'Rust часто меняется, поэтому каждый гайд держит официальные или полезные ссылки рядом с содержанием.',
      },
    ],
    officialFeed: 'Официальная лента',
    sections: (count) => formatRussianCount(count, 'раздел', 'раздела', 'разделов'),
    loadingPatchNotes: 'Загрузка последних патчноутов.',
    patchPreviewLabel: 'Последние патчноуты Rust',
  },
}

export function RustKnowledgePage() {
  const { language } = useLanguage()
  const copy = knowledgePageCopy[language]
  const knowledgeGuides = getKnowledgeGuides(language)
  const featuredGuide = knowledgeGuides.find((guide) => guide.featured) ?? knowledgeGuides[0]
  const regularGuides = knowledgeGuides.filter((guide) => guide.id !== featuredGuide.id)
  const patchNotesQuery = useQuery({
    queryKey: latestPatchNotesQueryKey,
    queryFn: ({ signal }) => getLatestPatchNotes(signal),
    staleTime: 15 * 60 * 1000,
  })
  const patchNotes = patchNotesQuery.data ?? []
  useAdsGate(true)

  return (
    <div className="page-stack knowledge-page">
      <section className="knowledge-hero-panel">
        <div className="knowledge-hero-panel__copy">
          <p className="page-eyebrow">{copy.eyebrow}</p>
          <h2 className="page-heading">{copy.heading}</h2>
          <p className="page-text">
            {copy.intro}
          </p>
          <div className="knowledge-hero-panel__stats" aria-label={copy.statsLabel}>
            <span>{copy.guides(knowledgeGuides.length)}</span>
            <span>{copy.officialSources}</span>
            <span>{copy.patchAwareNotes}</span>
          </div>
        </div>

        <div className="knowledge-hero-panel__art" aria-hidden="true">
          <img src="/rustiq-logo-320.webp" alt="" />
          <div>
            <span>RustIQ</span>
            <strong>{copy.hubTitle}</strong>
          </div>
        </div>
      </section>

      <section className="knowledge-home-grid" aria-label={copy.guideGridLabel}>
        <Link
          to={`/rust-knowledge/${featuredGuide.id}`}
          className="knowledge-home-card knowledge-home-card--featured"
        >
          <GuideCardMedia guide={featuredGuide} />
          <GuideCardContent
            guide={featuredGuide}
            isPatchNotesLoading={patchNotesQuery.isLoading}
            patchNotes={patchNotes}
            language={language}
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
              language={language}
            />
          </Link>
        ))}
      </section>

      <ContentAd placement="knowledge" />

      <SectionCard
        title={copy.howToTitle}
        description={copy.howToDescription}
      >
        <div className="knowledge-use-list">
          {copy.steps.map((step, index) => (
            <article key={step.title}>
              <span>{index + 1}</span>
              <div>
                <h3>{step.title}</h3>
                <p>{step.description}</p>
              </div>
            </article>
          ))}
        </div>
      </SectionCard>
    </div>
  )
}

function GuideCardContent({
  guide,
  isPatchNotesLoading,
  patchNotes,
  language,
}: {
  guide: KnowledgeGuide
  isPatchNotesLoading: boolean
  patchNotes: PatchNote[]
  language: Language
}) {
  const isPatchNotesGuide = guide.id === patchNotesGuideId
  const copy = knowledgePageCopy[language]

  return (
    <div className="knowledge-home-card__content">
      <span className="knowledge-home-card__label">{guide.label}</span>
      <h3>{guide.title}</h3>
      {isPatchNotesGuide ? (
        <PatchNoteCardPreview
          isLoading={isPatchNotesLoading}
          patchNotes={patchNotes}
          summary={guide.summary}
          language={language}
        />
      ) : (
        <p>{guide.summary}</p>
      )}
      <div className="knowledge-home-card__meta">
        <span>{guide.readTime}</span>
        <span>{isPatchNotesGuide ? copy.officialFeed : copy.sections(guide.sections.length || 1)}</span>
      </div>
    </div>
  )
}

function PatchNoteCardPreview({
  isLoading,
  patchNotes,
  summary,
  language,
}: {
  isLoading: boolean
  patchNotes: PatchNote[]
  summary: string
  language: Language
}) {
  const copy = knowledgePageCopy[language]

  if (isLoading) {
    return <p>{copy.loadingPatchNotes}</p>
  }

  if (patchNotes.length === 0) {
    return <p>{summary}</p>
  }

  return (
    <ol className="knowledge-patch-preview" aria-label={copy.patchPreviewLabel}>
      {patchNotes.slice(0, 3).map((patchNote) => (
        <li key={patchNote.url}>
          <span>{formatPatchNoteDate(patchNote.publishedAtUtc, language)}</span>
          <strong>{patchNote.title}</strong>
        </li>
      ))}
    </ol>
  )
}

function GuideCardMedia({ guide }: { guide: KnowledgeGuide }) {
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
