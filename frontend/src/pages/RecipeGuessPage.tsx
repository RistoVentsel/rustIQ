import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { SectionCard } from '../components/SectionCard'
import { ContentAd } from '../components/ContentAd'
import { StateNotice } from '../components/StateNotice'
import {
  cooldownMs,
  isCooldownActive,
  getDailyDateKey,
  pickBySeed,
  pickDistinctBySeed,
  readStoredState,
  shuffleBySeed,
  writeStoredState,
} from '../features/minigames/dailyPuzzle'
import {
  itemGameCopy,
  recipeOptionCount,
  recipeRevealDelayMs,
  recipeWinStreak,
} from '../features/minigames/itemGameCopy'
import { rustItems, type RustItem } from '../features/minigames/rustItemData'
import { useAdsGate, useNow } from '../hooks'
import { useLanguage } from '../i18n/useLanguage'

type RecipeRoundResult = {
  answerId: string
  guessedId: string
  correct: boolean
}

type StoredRecipeState = {
  dateKey: string
  puzzleKey?: string
  roundIndex: number
  streak: number
  bestStreak: number
  history: RecipeRoundResult[]
  completedAtUtc?: string
  nextAvailableAtUtc?: string
  solved?: boolean
}

type RecipeRound = {
  answer: RustItem
  options: RustItem[]
}

type RecipeReveal = {
  round: RecipeRound
  result: RecipeRoundResult
  nextState: StoredRecipeState
}

const storageKey = 'rustiq-minigames.recipe-guess'

function createFreshState(dateKey: string, bestStreak = 0): StoredRecipeState {
  return {
    dateKey,
    puzzleKey: `recipe:${Date.now()}`,
    roundIndex: 0,
    streak: 0,
    bestStreak,
    history: [],
  }
}

function normalizeState(state: StoredRecipeState | null, dateKey: string, nowMs: number): StoredRecipeState {
  if (!state) {
    return createFreshState(dateKey)
  }

  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  if (state.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
    return createFreshState(dateKey, state.bestStreak)
  }

  return {
    ...state,
    dateKey: state.dateKey || dateKey,
    puzzleKey: state.puzzleKey ?? `recipe:${dateKey}:legacy`,
    roundIndex: Math.max(state.roundIndex ?? 0, 0),
    streak: Math.max(state.streak ?? 0, 0),
    bestStreak: Math.max(state.bestStreak ?? 0, state.streak ?? 0, 0),
    history: Array.isArray(state.history) ? state.history.slice(0, 10) : [],
  }
}

/**
 * Builds one round. Decoys come from within one workbench tier of the answer so
 * the recipe itself is what separates the options, not an obvious tier mismatch.
 */
function buildRound(puzzleKey: string, roundIndex: number): RecipeRound | null {
  const seed = `${puzzleKey}:round:${roundIndex}`
  const answer = pickBySeed(rustItems, seed, (item) => item.id)

  if (!answer) {
    return null
  }

  const decoyPool = rustItems.filter((item) =>
    item.id !== answer.id && Math.abs(item.workbenchLevel - answer.workbenchLevel) <= 1)

  const decoys = pickDistinctBySeed(decoyPool, `${seed}:decoy`, recipeOptionCount - 1, (item) => item.id)

  if (decoys.length < recipeOptionCount - 1) {
    return null
  }

  return {
    answer,
    options: shuffleBySeed([answer, ...decoys], `${seed}:shuffle`),
  }
}

export function RecipeGuessPage() {
  const { language } = useLanguage()
  const copy = itemGameCopy[language]
  const gameCopy = copy.recipeGuess
  const nowMs = useNow()

  const [storedState, setStoredState] = useState<StoredRecipeState>(() =>
    normalizeState(readStoredState(storageKey), getDailyDateKey(), Date.now()))
  const [reveal, setReveal] = useState<RecipeReveal | null>(null)
  const revealTimeoutRef = useRef<number | null>(null)

  const dailyDateKey = getDailyDateKey()
  const puzzleKey = storedState.puzzleKey ?? `recipe:${dailyDateKey}`

  const currentRound = useMemo(
    () => buildRound(puzzleKey, storedState.roundIndex),
    [puzzleKey, storedState.roundIndex],
  )

  const cooldownActive = isCooldownActive(storedState.nextAvailableAtUtc, nowMs)
  const isRunClosed = cooldownActive || Boolean(storedState.completedAtUtc)

  const displayRound = reveal ? reveal.round : currentRound
  const visibleStreak = reveal ? reveal.nextState.streak : storedState.streak
  const visibleBestStreak = reveal ? reveal.nextState.bestStreak : storedState.bestStreak

  const historyRows = useMemo(() => storedState.history.map((result, index) => ({
    key: `${result.answerId}:${index}`,
    correct: result.correct,
    guessed: rustItems.find((item) => item.id === result.guessedId)?.name ?? result.guessedId,
    answer: rustItems.find((item) => item.id === result.answerId)?.name ?? result.answerId,
  })), [storedState.history])

  const clearRevealTimer = useCallback(() => {
    if (revealTimeoutRef.current !== null) {
      window.clearTimeout(revealTimeoutRef.current)
      revealTimeoutRef.current = null
    }
  }, [])

  const updateStoredState = useCallback((nextState: StoredRecipeState) => {
    setStoredState(nextState)
    writeStoredState(storageKey, nextState)
  }, [])

  useEffect(() => () => clearRevealTimer(), [clearRevealTimer])

  useEffect(() => {
    const nextAvailableMs = storedState.nextAvailableAtUtc ? Date.parse(storedState.nextAvailableAtUtc) : null
    if (storedState.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
      clearRevealTimer()
      setReveal(null)
      updateStoredState(createFreshState(dailyDateKey, storedState.bestStreak))
    }
  }, [clearRevealTimer, dailyDateKey, nowMs, storedState, updateStoredState])

  useAdsGate(Boolean(displayRound))

  const answerRound = (choice: RustItem) => {
    if (!currentRound || isRunClosed || reveal) {
      return
    }

    const correct = choice.id === currentRound.answer.id
    const nextStreak = correct ? storedState.streak + 1 : 0
    const nextBestStreak = Math.max(storedState.bestStreak, nextStreak)
    const hasCleared = correct && nextStreak >= recipeWinStreak
    const runIsOver = hasCleared || !correct
    const completedAtUtc = runIsOver ? new Date().toISOString() : undefined

    const result: RecipeRoundResult = {
      answerId: currentRound.answer.id,
      guessedId: choice.id,
      correct,
    }

    const nextState: StoredRecipeState = {
      dateKey: dailyDateKey,
      puzzleKey,
      roundIndex: runIsOver ? storedState.roundIndex : storedState.roundIndex + 1,
      streak: nextStreak,
      bestStreak: nextBestStreak,
      history: [result, ...storedState.history].slice(0, 10),
      solved: hasCleared,
      completedAtUtc,
      nextAvailableAtUtc: completedAtUtc
        ? new Date(Date.now() + cooldownMs).toISOString()
        : undefined,
    }

    setReveal({ round: currentRound, result, nextState })

    clearRevealTimer()
    revealTimeoutRef.current = window.setTimeout(() => {
      updateStoredState(nextState)
      setReveal(null)
      revealTimeoutRef.current = null
    }, recipeRevealDelayMs)
  }

  const getOptionClassName = (option: RustItem) => {
    if (!reveal) {
      return 'recipe-option'
    }

    if (option.id === reveal.round.answer.id) {
      return 'recipe-option recipe-option--correct'
    }

    if (option.id === reveal.result.guessedId) {
      return 'recipe-option recipe-option--wrong'
    }

    return 'recipe-option recipe-option--muted'
  }

  return (
    <div className="page-stack minigames-page">
      <Link to="/minigames" className="knowledge-back-link">{copy.backToMinigames}</Link>

      <SectionCard title={gameCopy.title} description={gameCopy.description}>
        {displayRound ? (
          <div className="recipe-game">
            <div className="guess-server-game__status">
              <div>
                <span className="minigame-option__status">{gameCopy.round(storedState.roundIndex + 1)}</span>
                <h3>{gameCopy.prompt}</h3>
              </div>
              <div className="guess-server-game__stats" aria-label={gameCopy.streak}>
                <div>
                  <span>{gameCopy.streak}</span>
                  <strong>{visibleStreak}/{recipeWinStreak}</strong>
                </div>
                <div>
                  <span>{gameCopy.best}</span>
                  <strong>{visibleBestStreak}</strong>
                </div>
              </div>
              <div className="guess-server-game__visual" aria-hidden="true">
                <img src="/rust-minigame-recipe-guess.png" alt="" />
                <div>
                  <span>{gameCopy.workbenchLabel}</span>
                  <strong>{copy.workbench[displayRound.answer.workbenchLevel]}</strong>
                </div>
              </div>
            </div>

            <div className="recipe-card">
              <span className="recipe-card__eyebrow">{gameCopy.ingredientsLabel}</span>
              <ul className="recipe-card__ingredients">
                {displayRound.answer.ingredients.map((ingredient) => (
                  <li key={ingredient.name}>
                    <strong>{ingredient.quantity}</strong>
                    <span>{ingredient.name}</span>
                  </li>
                ))}
              </ul>
              <p className="recipe-card__workbench">
                {gameCopy.workbenchLabel}: {copy.workbench[displayRound.answer.workbenchLevel]}
              </p>
            </div>

            {reveal ? (
              <StateNotice
                title={reveal.result.correct ? gameCopy.correct : gameCopy.incorrect}
                description={gameCopy.correctAnswerWas(reveal.round.answer.name)}
                tone={reveal.result.correct ? 'success' : 'warning'}
              />
            ) : null}

            {isRunClosed && !reveal ? (
              <StateNotice
                title={storedState.solved ? gameCopy.cleared(recipeWinStreak) : gameCopy.runEnded}
                description={`${gameCopy.bestStreak(storedState.bestStreak)} ${gameCopy.cooldownActive}`}
                tone={storedState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="recipe-options" role="group" aria-label={gameCopy.prompt}>
                {displayRound.options.map((option) => (
                  <button
                    key={option.id}
                    type="button"
                    className={getOptionClassName(option)}
                    onClick={() => answerRound(option)}
                    disabled={Boolean(reveal) || isRunClosed}
                  >
                    <strong>{option.name}</strong>
                    <small>{copy.categories[option.category]}</small>
                  </button>
                ))}
              </div>
            )}

            <div className="guess-item-history" aria-label={gameCopy.historyLabel}>
              <h4>{gameCopy.historyLabel}</h4>
              {historyRows.length === 0 ? (
                <p className="page-text">{gameCopy.emptyHistory}</p>
              ) : (
                <ul>
                  {historyRows.map((row) => (
                    <li
                      key={row.key}
                      className={row.correct ? 'guess-item-history__row guess-item-history__row--correct' : 'guess-item-history__row'}
                    >
                      <strong>{row.correct ? gameCopy.correct : gameCopy.incorrect}</strong>
                      <span>{gameCopy.historyLine(row.guessed, row.answer)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <p className="minigame-source-note">
              {gameCopy.sourceNote}{' '}
              <a href={displayRound.answer.wikiUrl} target="_blank" rel="noopener noreferrer nofollow">
                {displayRound.answer.wikiUrl.replace('https://', '')}
              </a>
            </p>
          </div>
        ) : null}
      </SectionCard>

      <ContentAd placement="minigames" hasContent={Boolean(displayRound)} />

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
