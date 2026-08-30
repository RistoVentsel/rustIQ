import { type FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'

import { SectionCard } from '../components/SectionCard'
import { ContentAd } from '../components/ContentAd'
import { StateNotice } from '../components/StateNotice'
import {
  cooldownMs,
  getCooldownParts,
  getDailyDateKey,
  isCooldownActive,
  pickBySeed,
  readStoredState,
  writeStoredState,
} from '../features/minigames/dailyPuzzle'
import {
  formatCooldownLabel,
  itemDifficultyOptions,
  itemGameCopy,
  itemHintCount,
  type ItemDifficulty,
} from '../features/minigames/itemGameCopy'
import {
  formatIngredient,
  getPuzzleItems,
  getTotalIngredientCount,
  type RustItem,
} from '../features/minigames/rustItemData'
import { useAdsGate, useNow } from '../hooks'
import { useLanguage } from '../i18n/useLanguage'

type StoredItemGuessState = {
  dateKey: string
  puzzleKey?: string
  attempts: string[]
  revealedHintCount?: number
  solved?: boolean
  revealedAnswer?: boolean
  completedAtUtc?: string
  nextAvailableAtUtc?: string
  answerId?: string
  previousAnswerId?: string
}

type RevealedHint = {
  label: string
  value: string
}

function getStorageKey(difficulty: ItemDifficulty) {
  return `rustiq-minigames.guess-item.${difficulty}`
}

function createFreshState(difficulty: ItemDifficulty, dateKey: string, previousAnswerId?: string): StoredItemGuessState {
  return {
    dateKey,
    puzzleKey: `${difficulty}:${Date.now()}`,
    previousAnswerId,
    revealedHintCount: 0,
    attempts: [],
  }
}

function normalizeState(
  state: StoredItemGuessState | null,
  dateKey: string,
  nowMs: number,
  difficulty: ItemDifficulty,
): StoredItemGuessState {
  if (!state) {
    return createFreshState(difficulty, dateKey)
  }

  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  if (state.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
    return createFreshState(difficulty, dateKey, state.answerId ?? state.previousAnswerId)
  }

  return {
    ...state,
    dateKey: state.dateKey || dateKey,
    puzzleKey: state.puzzleKey ?? `${difficulty}:${dateKey}:legacy`,
    attempts: Array.isArray(state.attempts) ? state.attempts : [],
    revealedHintCount: Math.min(Math.max(state.revealedHintCount ?? 0, 0), itemHintCount),
  }
}

function getPuzzleKey(state: StoredItemGuessState, difficulty: ItemDifficulty) {
  return state.puzzleKey ?? `${state.dateKey}:${difficulty}:guess-item`
}

function normalizeName(value: string) {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, '')
}

function getLargestIngredient(item: RustItem) {
  return item.ingredients.reduce((largest, ingredient) => (
    ingredient.quantity > largest.quantity ? ingredient : largest
  ), item.ingredients[0])
}

export function GuessItemPage() {
  const { language } = useLanguage()
  const copy = itemGameCopy[language]
  const gameCopy = copy.guessItem
  const nowMs = useNow()

  const [difficulty, setDifficulty] = useState<ItemDifficulty>('easy')
  const [guessInput, setGuessInput] = useState('')
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null)
  const [guessError, setGuessError] = useState<string | null>(null)
  const [isSuggestionsOpen, setIsSuggestionsOpen] = useState(false)
  const [storedState, setStoredState] = useState<StoredItemGuessState>(() =>
    normalizeState(readStoredState(getStorageKey('easy')), getDailyDateKey(), Date.now(), 'easy'))

  const dailyDateKey = getDailyDateKey()
  const selectedDifficulty = itemDifficultyOptions.find((option) => option.value === difficulty)
    ?? itemDifficultyOptions[0]

  const pool = useMemo(
    () => getPuzzleItems(selectedDifficulty.maxWorkbenchLevel),
    [selectedDifficulty.maxWorkbenchLevel],
  )

  const answer = useMemo(
    () => pickBySeed(
      pool,
      `${getPuzzleKey(storedState, difficulty)}:guess-item`,
      (item) => item.id,
      storedState.previousAnswerId,
    ),
    [pool, difficulty, storedState],
  )

  const difficultyStatuses = useMemo(() => Object.fromEntries(
    itemDifficultyOptions.map((option) => {
      const state = normalizeState(readStoredState(getStorageKey(option.value)), dailyDateKey, nowMs, option.value)
      const locked = isCooldownActive(state.nextAvailableAtUtc, nowMs) && Boolean(state.completedAtUtc)

      return [option.value, {
        isLocked: locked,
        label: locked ? formatCooldownLabel(getCooldownParts(state.nextAvailableAtUtc, nowMs), copy) : copy.available,
      }]
    }),
  ) as Record<ItemDifficulty, { isLocked: boolean, label: string }>, [copy, dailyDateKey, nowMs])

  const hints = useMemo<RevealedHint[]>(() => {
    if (!answer) {
      return []
    }

    const largest = getLargestIngredient(answer)
    const recycleValue = answer.recycle?.length
      ? answer.recycle.map(formatIngredient).join(', ')
      : null

    return [
      {
        label: gameCopy.hintWorkbenchLabel,
        value: gameCopy.hintWorkbenchValue(copy.workbench[answer.workbenchLevel]),
      },
      {
        label: gameCopy.hintIngredientsLabel,
        value: gameCopy.hintIngredientsValue(answer.ingredients.length, getTotalIngredientCount(answer)),
      },
      {
        label: gameCopy.hintMainIngredientLabel,
        value: gameCopy.hintMainIngredientValue(largest.quantity, largest.name),
      },
      {
        label: gameCopy.hintRecycleLabel,
        value: recycleValue ? gameCopy.hintRecycleValue(recycleValue) : gameCopy.hintRecycleUnknown,
      },
    ]
  }, [answer, copy.workbench, gameCopy])

  const revealedHints = hints.slice(0, storedState.revealedHintCount ?? 0)
  const hasRevealedAllHints = (storedState.revealedHintCount ?? 0) >= itemHintCount

  const attemptedItems = useMemo(
    () => storedState.attempts
      .map((itemId) => pool.find((item) => item.id === itemId))
      .filter((item): item is RustItem => Boolean(item)),
    [pool, storedState.attempts],
  )

  const suggestions = useMemo(() => {
    const normalizedInput = normalizeName(guessInput)
    const available = pool.filter((item) => !storedState.attempts.includes(item.id))

    if (!normalizedInput) {
      return available.slice(0, 8)
    }

    return available
      .filter((item) => normalizeName(item.name).includes(normalizedInput))
      .slice(0, 8)
  }, [pool, guessInput, storedState.attempts])

  const puzzleCooldownActive = isCooldownActive(storedState.nextAvailableAtUtc, nowMs)
  const isGameClosed = puzzleCooldownActive || Boolean(storedState.completedAtUtc)

  const updateStoredState = useCallback((nextState: StoredItemGuessState) => {
    setStoredState(nextState)
    writeStoredState(getStorageKey(difficulty), nextState)
  }, [difficulty])

  useEffect(() => {
    setStoredState(normalizeState(readStoredState(getStorageKey(difficulty)), dailyDateKey, Date.now(), difficulty))
    setGuessInput('')
    setSelectedItemId(null)
    setGuessError(null)
    setIsSuggestionsOpen(false)
  }, [difficulty, dailyDateKey])

  useEffect(() => {
    const nextAvailableMs = storedState.nextAvailableAtUtc ? Date.parse(storedState.nextAvailableAtUtc) : null
    if (storedState.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
      updateStoredState(createFreshState(difficulty, dailyDateKey, storedState.answerId ?? storedState.previousAnswerId))
      setGuessInput('')
      setSelectedItemId(null)
      setGuessError(null)
    }
  }, [dailyDateKey, difficulty, nowMs, storedState, updateStoredState])

  useAdsGate(Boolean(answer) && pool.length > 0)

  const submitGuess = (event: FormEvent) => {
    event.preventDefault()

    if (!answer || isGameClosed) {
      return
    }

    const guessed = selectedItemId
      ? pool.find((item) => item.id === selectedItemId)
      : pool.find((item) => normalizeName(item.name) === normalizeName(guessInput))

    if (!guessed) {
      setGuessError(gameCopy.chooseItem)
      return
    }

    if (storedState.attempts.includes(guessed.id)) {
      setGuessError(gameCopy.alreadyTried)
      return
    }

    setGuessError(null)
    setGuessInput('')
    setSelectedItemId(null)
    setIsSuggestionsOpen(false)

    const attempts = [guessed.id, ...storedState.attempts]

    if (guessed.id === answer.id) {
      updateStoredState({
        ...storedState,
        attempts,
        solved: true,
        answerId: answer.id,
        completedAtUtc: new Date().toISOString(),
        nextAvailableAtUtc: new Date(Date.now() + cooldownMs).toISOString(),
      })
      return
    }

    updateStoredState({
      ...storedState,
      attempts,
      revealedHintCount: Math.min((storedState.revealedHintCount ?? 0) + 1, itemHintCount),
    })
  }

  const revealAnswer = () => {
    if (!answer || isGameClosed) {
      return
    }

    updateStoredState({
      ...storedState,
      revealedAnswer: true,
      answerId: answer.id,
      completedAtUtc: new Date().toISOString(),
      nextAvailableAtUtc: new Date(Date.now() + cooldownMs).toISOString(),
    })
  }

  const describeAttempt = (item: RustItem) => {
    if (!answer) {
      return ''
    }

    const categoryPart = item.category === answer.category ? gameCopy.sameCategory : gameCopy.otherCategory
    const workbenchPart = item.workbenchLevel === answer.workbenchLevel
      ? gameCopy.sameWorkbench
      : item.workbenchLevel < answer.workbenchLevel
        ? gameCopy.higherWorkbench
        : gameCopy.lowerWorkbench

    return `${categoryPart} · ${workbenchPart}`
  }

  return (
    <div className="page-stack minigames-page">
      <Link to="/minigames" className="knowledge-back-link">{copy.backToMinigames}</Link>

      <SectionCard title={gameCopy.title} description={gameCopy.description}>
        <div className="difficulty-selector" aria-label={gameCopy.difficultyLabel}>
          {itemDifficultyOptions.map((option) => {
            const status = difficultyStatuses[option.value]
            const optionPool = getPuzzleItems(option.maxWorkbenchLevel)

            return (
              <button
                key={option.value}
                type="button"
                className={option.value === difficulty ? 'difficulty-button difficulty-button--active' : 'difficulty-button'}
                onClick={() => setDifficulty(option.value)}
              >
                <span>{copy.difficulties[option.value]}</span>
                <small>{copy.itemPool(optionPool.length)}</small>
                <strong className={status.isLocked ? 'difficulty-button__timer difficulty-button__timer--locked' : 'difficulty-button__timer'}>
                  {status.label}
                </strong>
              </button>
            )
          })}
        </div>

        {answer ? (
          <div className="guess-server-game">
            <div className="guess-server-game__status">
              <div>
                <span className="minigame-option__status">{copy.difficulties[difficulty]}</span>
                <h3>{gameCopy.puzzleTitle}</h3>
                <p className="page-text">{gameCopy.poolNote(pool.length)}</p>
              </div>
              <div className="guess-server-game__stats" aria-label={gameCopy.puzzleTitle}>
                <div>
                  <span>{gameCopy.attempts}</span>
                  <strong>{storedState.attempts.length}</strong>
                </div>
                <div>
                  <span>{gameCopy.hints}</span>
                  <strong>{1 + (storedState.revealedHintCount ?? 0)}/{1 + itemHintCount}</strong>
                </div>
              </div>
              <div className="guess-server-game__visual" aria-hidden="true">
                <img src="/rust-minigame-guess-item.png" alt="" />
                <div>
                  <span>{gameCopy.hints}</span>
                  <strong>{copy.categories[answer.category]}</strong>
                </div>
              </div>
            </div>

            <div className="guess-server-hint" aria-label={gameCopy.startingClueLabel}>
              <img className="guess-server-hint__emote" src="/rust-minigame-emote-hint.png" alt="" />
              <div>
                <span className="guess-server-hint__eyebrow">{gameCopy.startingClueLabel}</span>
                <h4>{gameCopy.startingClue(copy.categories[answer.category])}</h4>
              </div>
            </div>

            {revealedHints.length > 0 ? (
              <div className="guess-server-revealed-hints" aria-label={gameCopy.revealedHintsLabel}>
                {revealedHints.map((hint, index) => (
                  <div key={hint.label} className="guess-server-revealed-hint">
                    <span>{gameCopy.hintNumber(index + 2)}</span>
                    <strong>{hint.label}</strong>
                    <p>{hint.value}</p>
                  </div>
                ))}
              </div>
            ) : null}

            {isGameClosed ? (
              <StateNotice
                title={storedState.solved ? gameCopy.solved : storedState.revealedAnswer ? gameCopy.answerRevealed : gameCopy.cooldownActive}
                description={(
                  <span className="guess-server-answer-notice">
                    <span>{storedState.revealedAnswer ? gameCopy.revealedAnswer : gameCopy.answer}</span>
                    <strong>{answer.name}</strong>
                  </span>
                )}
                tone={storedState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="guess-server-actions">
                <form className="guess-server-form" onSubmit={submitGuess}>
                  <label className="guess-server-input">
                    <span>{gameCopy.yourGuess}</span>
                    <input
                      type="search"
                      value={guessInput}
                      onChange={(event) => {
                        setGuessInput(event.target.value)
                        setSelectedItemId(null)
                        setGuessError(null)
                        setIsSuggestionsOpen(true)
                      }}
                      onFocus={() => setIsSuggestionsOpen(true)}
                      onBlur={() => window.setTimeout(() => setIsSuggestionsOpen(false), 120)}
                      placeholder={gameCopy.placeholder}
                      autoComplete="off"
                    />
                    {isSuggestionsOpen && suggestions.length > 0 ? (
                      <div className="guess-server-suggestions" role="listbox">
                        {suggestions.map((item) => (
                          <button
                            key={item.id}
                            type="button"
                            className="guess-server-suggestion"
                            onMouseDown={(event) => event.preventDefault()}
                            onClick={() => {
                              setSelectedItemId(item.id)
                              setGuessInput(item.name)
                              setIsSuggestionsOpen(false)
                            }}
                          >
                            <span>{item.name}</span>
                            <small>{copy.categories[item.category]} | {copy.workbench[item.workbenchLevel]}</small>
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </label>

                  <button type="submit" className="button-link">{gameCopy.submit}</button>
                </form>

                {hasRevealedAllHints ? (
                  <button type="button" className="button-link button-link--secondary" onClick={revealAnswer}>
                    {gameCopy.revealItem}
                  </button>
                ) : null}
              </div>
            )}

            {guessError ? <p className="guess-server-error">{guessError}</p> : null}

            <div className="guess-item-history" aria-label={gameCopy.historyLabel}>
              <h4>{gameCopy.historyLabel}</h4>
              {attemptedItems.length === 0 ? (
                <p className="page-text">{gameCopy.emptyHistory}</p>
              ) : (
                <ul>
                  {attemptedItems.map((item) => (
                    <li key={item.id} className={item.id === answer.id ? 'guess-item-history__row guess-item-history__row--correct' : 'guess-item-history__row'}>
                      <strong>{item.name}</strong>
                      <span>{describeAttempt(item)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <p className="minigame-source-note">
              {gameCopy.sourceNote}{' '}
              <a href={answer.wikiUrl} target="_blank" rel="noopener noreferrer nofollow">
                {answer.wikiUrl.replace('https://', '')}
              </a>
            </p>
          </div>
        ) : null}
      </SectionCard>

      <ContentAd placement="minigames" hasContent={Boolean(answer)} />

      <SectionCard title={gameCopy.howToPlayTitle}>
        <ul className="minigame-how-to-play">
          {gameCopy.howToPlay.map((line) => (
            <li key={line.slice(0, 24)}>{line}</li>
          ))}
        </ul>
      </SectionCard>
    </div>
  )
}
