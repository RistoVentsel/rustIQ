import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'

import { getLatestPatchNotes } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { formatPatchNoteDate, latestPatchNotesQueryKey } from '../features/knowledge/patchNotesPresentation'
import {
  breedingRules,
  breedingWorkflow,
  getKnowledgeGuide,
  idealGeneBuilds,
  keybindCommands,
  plantGenes,
  patchNotesGuideId,
  type KnowledgeGuide,
} from '../features/knowledge/rustKnowledgeContent'

export function RustKnowledgeTopicPage() {
  const { topicId } = useParams()
  const guide = getKnowledgeGuide(topicId)

  if (!guide) {
    return (
      <div className="page-stack knowledge-detail-page">
        <Link className="knowledge-back-link" to="/rust-knowledge">Back to Rust Knowledge</Link>
        <SectionCard
          title="Guide not found"
          description="This RustIQ guide does not exist yet."
        >
          <Link className="button-link" to="/rust-knowledge">Open guide index</Link>
        </SectionCard>
      </div>
    )
  }

  return (
    <div className="page-stack knowledge-detail-page">
      <Link className="knowledge-back-link" to="/rust-knowledge">Back to Rust Knowledge</Link>
      <GuideHero guide={guide} />

      {guide.id === 'keybinds' ? (
        <KeybindGuide guide={guide} />
      ) : guide.id === 'farming-plant-genetics' ? (
        <PlantGeneticsGuide guide={guide} />
      ) : guide.id === patchNotesGuideId ? (
        <PatchNotesGuide guide={guide} />
      ) : (
        <StandardGuide guide={guide} />
      )}
    </div>
  )
}

function GuideHero({ guide }: { guide: KnowledgeGuide }) {
  return (
    <section className="knowledge-detail-hero">
      <div className="knowledge-detail-hero__copy">
        <p className="page-eyebrow">{guide.label}</p>
        <h2 className="page-heading">{guide.title}</h2>
        <p className="page-text">{guide.description}</p>
      </div>

      <div className="knowledge-detail-hero__visual" aria-hidden="true">
        <img src={guide.image} alt="" />
        <div>
          <span>{guide.visualLabel}</span>
          <strong>{guide.visualStat}</strong>
        </div>
      </div>

      <aside className="knowledge-detail-hero__panel" aria-label="Guide summary">
        <span>{guide.readTime}</span>
        <h3>RustIQ angle</h3>
        <p>{guide.rustIqAngle}</p>
        {guide.id === 'farming-plant-genetics' ? (
          <a
            className="knowledge-tool-link"
            href="https://rustbreeder.com/"
            target="_blank"
            rel="noreferrer"
          >
            Open RustBreeder calculator
          </a>
        ) : null}
      </aside>
    </section>
  )
}

function StandardGuide({ guide }: { guide: KnowledgeGuide }) {
  return (
    <>
      <SectionCard
        title="Key Takeaways"
        description="The short version before the details."
      >
        <div className="knowledge-takeaway-grid">
          {guide.keyTakeaways.map((takeaway) => (
            <article key={takeaway} className="knowledge-takeaway">
              <span />
              <p>{takeaway}</p>
            </article>
          ))}
        </div>
      </SectionCard>

      {guide.sections.map((section) => (
        <SectionCard
          key={section.title}
          title={section.title}
          description={section.intro}
        >
          <div className="knowledge-detail-grid">
            {section.items.map((item) => (
              <article key={item.title} className="knowledge-detail-item">
                <h3>{item.title}</h3>
                <p>{item.detail}</p>
                {item.sourceUrl ? (
                  <a href={item.sourceUrl} target="_blank" rel="noreferrer">
                    {item.sourceLabel ?? 'Source'}
                  </a>
                ) : null}
              </article>
            ))}
          </div>
        </SectionCard>
      ))}

      <GuideSources guide={guide} />
    </>
  )
}

function PatchNotesGuide({ guide }: { guide: KnowledgeGuide }) {
  const patchNotesQuery = useQuery({
    queryKey: latestPatchNotesQueryKey,
    queryFn: ({ signal }) => getLatestPatchNotes(signal),
    staleTime: 15 * 60 * 1000,
  })
  const patchNotes = patchNotesQuery.data ?? []

  return (
    <>
      <SectionCard
        title="Latest Patch Notes"
        description="The newest official Rust update posts from Facepunch."
      >
        {patchNotesQuery.isLoading ? (
          <StateNotice
            title="Loading patch notes"
            description="Reading the official Rust feed."
          />
        ) : null}

        {patchNotesQuery.isError ? (
          <StateNotice
            tone="warning"
            title="Patch notes unavailable"
            description="The official Rust feed could not be loaded right now."
            detail={patchNotesQuery.error instanceof Error ? patchNotesQuery.error.message : undefined}
          />
        ) : null}

        {!patchNotesQuery.isLoading && !patchNotesQuery.isError && patchNotes.length === 0 ? (
          <StateNotice
            tone="warning"
            title="No patch notes found"
            description="The official Rust feed returned no entries."
          />
        ) : null}

        {patchNotes.length > 0 ? (
          <div className="patch-note-list">
            {patchNotes.slice(0, 3).map((patchNote) => (
              <article
                key={patchNote.url}
                className={patchNote.imageUrl ? 'patch-note-item' : 'patch-note-item patch-note-item--text-only'}
              >
                {patchNote.imageUrl ? (
                  <img src={patchNote.imageUrl} alt="" loading="lazy" />
                ) : null}
                <div>
                  <span>{formatPatchNoteDate(patchNote.publishedAtUtc)}</span>
                  <h3>{patchNote.title}</h3>
                  <p>{patchNote.summary}</p>
                  <a href={patchNote.url} target="_blank" rel="noreferrer">
                    Read patch note
                  </a>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </SectionCard>

      <GuideSources guide={guide} />
    </>
  )
}

function KeybindGuide({ guide }: { guide: KnowledgeGuide }) {
  const [copiedCommand, setCopiedCommand] = useState<string | null>(null)

  async function copyCommandToClipboard(command: string) {
    await navigator.clipboard.writeText(command)
    setCopiedCommand(command)
    window.setTimeout(() => {
      setCopiedCommand((currentCommand) => currentCommand === command ? null : currentCommand)
    }, 1600)
  }

  return (
    <>
      <SectionCard
        title="How To Use"
        description="Open the Rust F1 console, paste one command into the blue command box, then press Enter."
      >
        <div className="knowledge-takeaway-grid">
          {guide.keyTakeaways.map((takeaway) => (
            <article key={takeaway} className="knowledge-takeaway">
              <span />
              <p>{takeaway}</p>
            </article>
          ))}
        </div>
      </SectionCard>

      <SectionCard
        title="Copy Keybinds"
        description="Each button copies only the command, without the explanation text."
      >
        <div className="keybind-list">
          {keybindCommands.map((keybind) => (
            <article key={keybind.command} className="keybind-item">
              <div className="keybind-item__command">
                <code>{keybind.command}</code>
              </div>
              <p>{keybind.description}</p>
              <button
                type="button"
                className="copy-button keybind-item__copy"
                onClick={() => {
                  void copyCommandToClipboard(keybind.command)
                }}
                aria-label={`Copy keybind command ${keybind.command}`}
              >
                {copiedCommand === keybind.command ? 'Copied' : 'Copy'}
              </button>
            </article>
          ))}
        </div>
      </SectionCard>

      <GuideSources guide={guide} />
    </>
  )
}

function PlantGeneticsGuide({ guide }: { guide: KnowledgeGuide }) {
  return (
    <>
      <SectionCard
        title="Gene Meanings"
        description="Each plant has six slots. These five letters are what can appear in those slots."
      >
        <div className="plant-gene-grid" aria-label="Rust plant gene meanings">
          {plantGenes.map((gene) => (
            <article key={gene.gene} className={`plant-gene-card plant-gene-card--${gene.tone}`}>
              <div className="plant-gene-card__header">
                <strong>{gene.gene}</strong>
                <div>
                  <h3>{gene.name}</h3>
                  <span>{gene.tone === 'positive' ? 'Production gene' : gene.tone === 'negative' ? 'Bad gene' : 'Empty gene'}</span>
                </div>
              </div>
              <p>{gene.role}</p>
              <p>{gene.inPractice}</p>
              <p><strong>Watch out:</strong> {gene.commonMistake}</p>
            </article>
          ))}
        </div>
      </SectionCard>

      <SectionCard
        title="How Crossbreeding Works"
        description="Think in six columns. Each gene position is decided separately."
      >
        <div className="plant-rule-list">
          {breedingRules.map((rule) => (
            <article key={rule.title} className="plant-rule">
              <h3>{rule.title}</h3>
              <p>{rule.detail}</p>
            </article>
          ))}
        </div>
      </SectionCard>

      <section className="plant-example">
        <div>
          <span className="utility-tile__eyebrow">Example</span>
          <h3>Why two matching positives beat one bad gene</h3>
          <p>
            Imagine you are fixing slot 4 on the target plant. Among adjacent donor plants, slot 4 contains G, G, Y, and W. The two G donors stack together, the Y stands alone, and the W is heavy but alone. The slot is likely to become G because the matching G pressure is stronger than the single W.
          </p>
        </div>
        <div className="plant-example__math" aria-label="Crossbreeding slot example">
          <span>Slot 4 donors</span>
          <strong>G + G + Y + W</strong>
          <span>Likely winner</span>
          <strong>G</strong>
        </div>
      </section>

      <div className="knowledge-detail-split">
        <SectionCard
          title="Good Target Clones"
          description="The best clone depends on how controlled the farm is and how often someone harvests it."
        >
          <div className="plant-build-list">
            {idealGeneBuilds.map((build) => (
              <article key={build.title} className="plant-build">
                <strong>{build.title}</strong>
                <p>{build.detail}</p>
              </article>
            ))}
          </div>
        </SectionCard>

        <SectionCard
          title="Breeding Workflow"
          description="Use this when starting from random seeds instead of buying a perfect clone."
        >
          <ol className="plant-workflow">
            {breedingWorkflow.map((step, index) => (
              <li key={step.title}>
                <span>{index + 1}</span>
                <div>
                  <strong>{step.title}</strong>
                  <p>{step.detail}</p>
                </div>
              </li>
            ))}
          </ol>
        </SectionCard>
      </div>

      <GuideSources guide={guide} />
    </>
  )
}

function GuideSources({ guide }: { guide: KnowledgeGuide }) {
  return (
    <SectionCard
      title="Sources"
      description="Rust changes often; these are the pages used for this guide."
    >
      <div className="knowledge-source-list">
        {guide.sources.map((source) => (
          <a key={source.url} href={source.url} target="_blank" rel="noreferrer">
            {source.label}
          </a>
        ))}
      </div>
    </SectionCard>
  )
}
