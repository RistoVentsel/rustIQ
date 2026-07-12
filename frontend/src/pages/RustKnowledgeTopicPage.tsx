import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'

import { getLatestPatchNotes } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import { formatPatchNoteDate, latestPatchNotesQueryKey } from '../features/knowledge/patchNotesPresentation'
import {
  getBreedingRules,
  getBreedingWorkflow,
  getIdealGeneBuilds,
  getKeybindCommands,
  getKnowledgeGuide,
  getPlantGenes,
  patchNotesGuideId,
  type KnowledgeGuide,
} from '../features/knowledge/rustKnowledgeContent'
import type { Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

const knowledgeTopicCopy: Record<Language, {
  backToKnowledge: string
  guideNotFoundTitle: string
  guideNotFoundDescription: string
  openGuideIndex: string
  guideSummaryLabel: string
  rustIqAngle: string
  openRustBreeder: string
  keyTakeaways: string
  keyTakeawaysDescription: string
  source: string
  latestPatchNotes: string
  latestPatchNotesDescription: string
  loadingPatchNotes: string
  readingFeed: string
  patchNotesUnavailable: string
  feedUnavailable: string
  noPatchNotesFound: string
  noFeedEntries: string
  readPatchNote: string
  howToUse: string
  keybindHowToUseDescription: string
  copyKeybinds: string
  copyKeybindsDescription: string
  copied: string
  copy: string
  copyKeybindCommand: (command: string) => string
  geneMeanings: string
  geneMeaningsDescription: string
  geneGridLabel: string
  productionGene: string
  badGene: string
  emptyGene: string
  watchOut: string
  crossbreedingTitle: string
  crossbreedingDescription: string
  example: string
  exampleTitle: string
  exampleDescription: string
  slotDonors: string
  likelyWinner: string
  goodTargetClones: string
  goodTargetClonesDescription: string
  breedingWorkflow: string
  breedingWorkflowDescription: string
  sources: string
  sourcesDescription: string
}> = {
  en: {
    backToKnowledge: 'Back to Rust Knowledge',
    guideNotFoundTitle: 'Guide not found',
    guideNotFoundDescription: 'This RustIQ guide does not exist yet.',
    openGuideIndex: 'Open guide index',
    guideSummaryLabel: 'Guide summary',
    rustIqAngle: 'RustIQ angle',
    openRustBreeder: 'Open RustBreeder calculator',
    keyTakeaways: 'Key Takeaways',
    keyTakeawaysDescription: 'The short version before the details.',
    source: 'Source',
    latestPatchNotes: 'Latest Patch Notes',
    latestPatchNotesDescription: 'The newest official Rust update posts from Facepunch.',
    loadingPatchNotes: 'Loading patch notes',
    readingFeed: 'Reading the official Rust feed.',
    patchNotesUnavailable: 'Patch notes unavailable',
    feedUnavailable: 'The official Rust feed could not be loaded right now.',
    noPatchNotesFound: 'No patch notes found',
    noFeedEntries: 'The official Rust feed returned no entries.',
    readPatchNote: 'Read patch note',
    howToUse: 'How To Use',
    keybindHowToUseDescription: 'Open the Rust F1 console, paste one command into the blue command box, then press Enter.',
    copyKeybinds: 'Copy Keybinds',
    copyKeybindsDescription: 'Each button copies only the command, without the explanation text.',
    copied: 'Copied',
    copy: 'Copy',
    copyKeybindCommand: (command) => `Copy keybind command ${command}`,
    geneMeanings: 'Gene Meanings',
    geneMeaningsDescription: 'Each plant has six slots. These five letters are what can appear in those slots.',
    geneGridLabel: 'Rust plant gene meanings',
    productionGene: 'Production gene',
    badGene: 'Bad gene',
    emptyGene: 'Empty gene',
    watchOut: 'Watch out:',
    crossbreedingTitle: 'How Crossbreeding Works',
    crossbreedingDescription: 'Think in six columns. Each gene position is decided separately.',
    example: 'Example',
    exampleTitle: 'Why two matching positives beat one bad gene',
    exampleDescription: 'Imagine you are fixing slot 4 on the target plant. Among adjacent donor plants, slot 4 contains G, G, Y, and W. The two G donors stack together, the Y stands alone, and the W is heavy but alone. The slot is likely to become G because the matching G pressure is stronger than the single W.',
    slotDonors: 'Slot 4 donors',
    likelyWinner: 'Likely winner',
    goodTargetClones: 'Good Target Clones',
    goodTargetClonesDescription: 'The best clone depends on how controlled the farm is and how often someone harvests it.',
    breedingWorkflow: 'Breeding Workflow',
    breedingWorkflowDescription: 'Use this when starting from random seeds instead of buying a perfect clone.',
    sources: 'Sources',
    sourcesDescription: 'Rust changes often; these are the pages used for this guide.',
  },
  ru: {
    backToKnowledge: 'Назад к базе знаний Rust',
    guideNotFoundTitle: 'Гайд не найден',
    guideNotFoundDescription: 'Такого гайда RustIQ пока нет.',
    openGuideIndex: 'Открыть индекс гайдов',
    guideSummaryLabel: 'Сводка гайда',
    rustIqAngle: 'Подход RustIQ',
    openRustBreeder: 'Открыть калькулятор RustBreeder',
    keyTakeaways: 'Главные выводы',
    keyTakeawaysDescription: 'Короткая версия перед деталями.',
    source: 'Источник',
    latestPatchNotes: 'Последние патчноуты',
    latestPatchNotesDescription: 'Самые новые официальные посты обновлений Rust от Facepunch.',
    loadingPatchNotes: 'Загрузка патчноутов',
    readingFeed: 'Читаем официальную ленту Rust.',
    patchNotesUnavailable: 'Патчноуты недоступны',
    feedUnavailable: 'Официальную ленту Rust сейчас не удалось загрузить.',
    noPatchNotesFound: 'Патчноуты не найдены',
    noFeedEntries: 'Официальная лента Rust не вернула записей.',
    readPatchNote: 'Читать патчноут',
    howToUse: 'Как использовать',
    keybindHowToUseDescription: 'Откройте F1-консоль Rust, вставьте команду в синее поле и нажмите Enter.',
    copyKeybinds: 'Копировать бинды',
    copyKeybindsDescription: 'Каждая кнопка копирует только команду, без поясняющего текста.',
    copied: 'Скопировано',
    copy: 'Копировать',
    copyKeybindCommand: (command) => `Копировать bind-команду ${command}`,
    geneMeanings: 'Значения генов',
    geneMeaningsDescription: 'У каждого растения шесть слотов. В них могут появляться эти пять букв.',
    geneGridLabel: 'Значения генов растений Rust',
    productionGene: 'Производственный ген',
    badGene: 'Плохой ген',
    emptyGene: 'Пустой ген',
    watchOut: 'Осторожно:',
    crossbreedingTitle: 'Как работает скрещивание',
    crossbreedingDescription: 'Думайте шестью колонками. Каждая позиция гена считается отдельно.',
    example: 'Пример',
    exampleTitle: 'Почему два одинаковых плюса сильнее одного плохого гена',
    exampleDescription: 'Представьте, что вы исправляете четвертый слот целевого растения. У соседних доноров в четвертом слоте стоят G, G, Y и W. Два G складываются вместе, Y стоит отдельно, а W тяжелый, но один. Слот, скорее всего, станет G, потому что давление одинаковых G сильнее одиночного W.',
    slotDonors: 'Доноры слота 4',
    likelyWinner: 'Вероятный победитель',
    goodTargetClones: 'Хорошие целевые клоны',
    goodTargetClonesDescription: 'Лучший клон зависит от контроля фермы и частоты сбора.',
    breedingWorkflow: 'Рабочий процесс селекции',
    breedingWorkflowDescription: 'Используйте это, когда начинаете со случайных семян, а не покупаете идеальный клон.',
    sources: 'Источники',
    sourcesDescription: 'Rust часто меняется; эти страницы использованы для гайда.',
  },
}

export function RustKnowledgeTopicPage() {
  const { topicId } = useParams()
  const { language } = useLanguage()
  const copy = knowledgeTopicCopy[language]
  const guide = getKnowledgeGuide(topicId, language)

  if (!guide) {
    return (
      <div className="page-stack knowledge-detail-page">
        <Link className="knowledge-back-link" to="/rust-knowledge">{copy.backToKnowledge}</Link>
        <SectionCard
          title={copy.guideNotFoundTitle}
          description={copy.guideNotFoundDescription}
        >
          <Link className="button-link" to="/rust-knowledge">{copy.openGuideIndex}</Link>
        </SectionCard>
      </div>
    )
  }

  return (
    <div className="page-stack knowledge-detail-page">
      <Link className="knowledge-back-link" to="/rust-knowledge">{copy.backToKnowledge}</Link>
      <GuideHero guide={guide} language={language} />

      {guide.id === 'keybinds' ? (
        <KeybindGuide guide={guide} language={language} />
      ) : guide.id === 'farming-plant-genetics' ? (
        <PlantGeneticsGuide guide={guide} language={language} />
      ) : guide.id === patchNotesGuideId ? (
        <PatchNotesGuide guide={guide} language={language} />
      ) : (
        <StandardGuide guide={guide} language={language} />
      )}
    </div>
  )
}

function GuideHero({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]

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

      <aside className="knowledge-detail-hero__panel" aria-label={copy.guideSummaryLabel}>
        <span>{guide.readTime}</span>
        <h3>{copy.rustIqAngle}</h3>
        <p>{guide.rustIqAngle}</p>
        {guide.id === 'farming-plant-genetics' ? (
          <a
            className="knowledge-tool-link"
            href="https://rustbreeder.com/"
            target="_blank"
            rel="noreferrer"
          >
            {copy.openRustBreeder}
          </a>
        ) : null}
      </aside>
    </section>
  )
}

function StandardGuide({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]

  return (
    <>
      <SectionCard
        title={copy.keyTakeaways}
        description={copy.keyTakeawaysDescription}
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
                    {item.sourceLabel ?? copy.source}
                  </a>
                ) : null}
              </article>
            ))}
          </div>
        </SectionCard>
      ))}

      <GuideSources guide={guide} language={language} />
    </>
  )
}

function PatchNotesGuide({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]
  const patchNotesQuery = useQuery({
    queryKey: latestPatchNotesQueryKey,
    queryFn: ({ signal }) => getLatestPatchNotes(signal),
    staleTime: 15 * 60 * 1000,
  })
  const patchNotes = patchNotesQuery.data ?? []

  return (
    <>
      <SectionCard
        title={copy.latestPatchNotes}
        description={copy.latestPatchNotesDescription}
      >
        {patchNotesQuery.isLoading ? (
          <StateNotice
            title={copy.loadingPatchNotes}
            description={copy.readingFeed}
          />
        ) : null}

        {patchNotesQuery.isError ? (
          <StateNotice
            tone="warning"
            title={copy.patchNotesUnavailable}
            description={copy.feedUnavailable}
            detail={patchNotesQuery.error instanceof Error ? patchNotesQuery.error.message : undefined}
          />
        ) : null}

        {!patchNotesQuery.isLoading && !patchNotesQuery.isError && patchNotes.length === 0 ? (
          <StateNotice
            tone="warning"
            title={copy.noPatchNotesFound}
            description={copy.noFeedEntries}
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
                  <span>{formatPatchNoteDate(patchNote.publishedAtUtc, language)}</span>
                  <h3>{patchNote.title}</h3>
                  <p>{patchNote.summary}</p>
                  <a href={patchNote.url} target="_blank" rel="noreferrer">
                    {copy.readPatchNote}
                  </a>
                </div>
              </article>
            ))}
          </div>
        ) : null}
      </SectionCard>

      <GuideSources guide={guide} language={language} />
    </>
  )
}

function KeybindGuide({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]
  const keybindCommands = getKeybindCommands(language)
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
        title={copy.howToUse}
        description={copy.keybindHowToUseDescription}
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
        title={copy.copyKeybinds}
        description={copy.copyKeybindsDescription}
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
                aria-label={copy.copyKeybindCommand(keybind.command)}
              >
                {copiedCommand === keybind.command ? copy.copied : copy.copy}
              </button>
            </article>
          ))}
        </div>
      </SectionCard>

      <GuideSources guide={guide} language={language} />
    </>
  )
}

function PlantGeneticsGuide({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]
  const plantGenes = getPlantGenes(language)
  const breedingRules = getBreedingRules(language)
  const idealGeneBuilds = getIdealGeneBuilds(language)
  const breedingWorkflow = getBreedingWorkflow(language)

  return (
    <>
      <SectionCard
        title={copy.geneMeanings}
        description={copy.geneMeaningsDescription}
      >
        <div className="plant-gene-grid" aria-label={copy.geneGridLabel}>
          {plantGenes.map((gene) => (
            <article key={gene.gene} className={`plant-gene-card plant-gene-card--${gene.tone}`}>
              <div className="plant-gene-card__header">
                <strong>{gene.gene}</strong>
                <div>
                  <h3>{gene.name}</h3>
                  <span>{gene.tone === 'positive' ? copy.productionGene : gene.tone === 'negative' ? copy.badGene : copy.emptyGene}</span>
                </div>
              </div>
              <p>{gene.role}</p>
              <p>{gene.inPractice}</p>
              <p><strong>{copy.watchOut}</strong> {gene.commonMistake}</p>
            </article>
          ))}
        </div>
      </SectionCard>

      <SectionCard
        title={copy.crossbreedingTitle}
        description={copy.crossbreedingDescription}
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
          <span className="utility-tile__eyebrow">{copy.example}</span>
          <h3>{copy.exampleTitle}</h3>
          <p>
            {copy.exampleDescription}
          </p>
        </div>
        <div className="plant-example__math" aria-label="Crossbreeding slot example">
          <span>{copy.slotDonors}</span>
          <strong>G + G + Y + W</strong>
          <span>{copy.likelyWinner}</span>
          <strong>G</strong>
        </div>
      </section>

      <div className="knowledge-detail-split">
        <SectionCard
          title={copy.goodTargetClones}
          description={copy.goodTargetClonesDescription}
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
          title={copy.breedingWorkflow}
          description={copy.breedingWorkflowDescription}
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

      <GuideSources guide={guide} language={language} />
    </>
  )
}

function GuideSources({ guide, language }: { guide: KnowledgeGuide, language: Language }) {
  const copy = knowledgeTopicCopy[language]

  return (
    <SectionCard
      title={copy.sources}
      description={copy.sourcesDescription}
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
