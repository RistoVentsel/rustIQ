import { useQuery } from '@tanstack/react-query'
import { type FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

import { ApiError, getServers, type ServerListItem } from '../api'
import { SectionCard } from '../components/SectionCard'
import { StateNotice } from '../components/StateNotice'
import {
  formatCountry,
  getRustServerTypeLabel,
  getServerDisplayName,
} from '../features/servers/serverPresentation'
import { useNow } from '../hooks'

type GuessDifficulty = 'easy' | 'medium' | 'hard'
type ClueKind = 'region' | 'type' | 'players' | 'maxPlayers' | 'wipe' | 'rank'
type HigherLowerMetric = 'averagePlayers' | 'currentPlayers' | 'rank'
type HigherLowerChoice = 'higher' | 'lower'

type GuessClue = {
  label: string
  value: string
  tone?: 'correct' | 'wrong' | 'neutral'
}

type StoredGuessState = {
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

type DifficultyStatus = {
  isLocked: boolean
  label: string
}

type StoredHigherLowerState = {
  dateKey: string
  puzzleKey?: string
  roundIndex: number
  streak: number
  bestStreak: number
  history: HigherLowerRoundResult[]
  completedAtUtc?: string
  nextAvailableAtUtc?: string
  solved?: boolean
}

type HigherLowerRoundResult = {
  leftId: string
  rightId: string
  choice: HigherLowerChoice
  correct: boolean
  metric: HigherLowerMetric
  leftValue: number
  rightValue: number
}

type HigherLowerRoundReveal = {
  left: ServerListItem
  right: ServerListItem
  result: HigherLowerRoundResult
  nextState: StoredHigherLowerState
}

const gameCards = [
  {
    title: 'Guess the Server',
    status: 'Playable',
    description: 'Identify a ranked Rust server from comparison clues.',
    image: '/rust-minigame-guess-server.png',
    emote: '/rust-minigame-emote-mystery.png',
    meta: 'Top 250 pool',
    path: '/minigames/guess-the-server',
  },
  {
    title: 'Higher / Lower',
    status: 'Playable',
    description: 'Pick which server has the higher population, peak, or rank.',
    image: '/rust-minigame-higher-lower.png',
    emote: '/rust-minigame-emote-streak.png',
    meta: 'Population reads',
    path: '/minigames/higher-lower',
  },
  {
    title: 'Guess the Item',
    status: 'Coming soon',
    description: 'Use workbench, category, cost, and recycle clues.',
    image: '/rust-minigame-guess-item.png',
    emote: '/rust-minigame-emote-hint.png',
    meta: 'Item logic',
    path: '/minigames/guess-the-item',
  },
  {
    title: 'Recipe Guess',
    status: 'Coming soon',
    description: 'See ingredients and name the item they craft.',
    image: '/rust-minigame-recipe-guess.png',
    emote: '/rust-minigame-emote-correct.png',
    meta: 'Craft clues',
    path: '/minigames/recipe-guess',
  },
] as const

const difficulties: Array<{ value: GuessDifficulty, label: string, poolSize: number }> = [
  { value: 'easy', label: 'Easy', poolSize: 50 },
  { value: 'medium', label: 'Medium', poolSize: 100 },
  { value: 'hard', label: 'Hard', poolSize: 250 },
]

const progressiveHintCount = 4
const cooldownMs = 24 * 60 * 60 * 1000
const higherLowerWinStreak = 10
const higherLowerRevealDelayMs = 1600
const higherLowerMetrics: Array<{ value: HigherLowerMetric, label: string, detail: string }> = [
  { value: 'averagePlayers', label: 'Average pop', detail: 'Ranked average players' },
  { value: 'currentPlayers', label: 'Live players', detail: 'Current online count' },
  { value: 'rank', label: 'Rank', detail: 'Lower number wins' },
]

export function MinigamesPage() {
  return (
    <div className="page-stack minigames-page">
      <MinigamesPageHeader />
      <MinigameTodayPanel />
      <MinigameCardGrid />
    </div>
  )
}

export function GuessServerMinigamePage() {
  return <MinigameExperience activeGame="guess" />
}

export function HigherLowerMinigamePage() {
  return <MinigameExperience activeGame="higherLower" />
}

export function GuessItemMinigamePage() {
  return <ComingSoonMinigamePage gameTitle="Guess the Item" />
}

export function RecipeGuessMinigamePage() {
  return <ComingSoonMinigamePage gameTitle="Recipe Guess" />
}

function MinigamesPageHeader() {
  return (
    <div className="page-copy">
      <p className="page-eyebrow">Minigames</p>
      <h2 className="page-heading">Daily RustIQ challenges</h2>
      <p className="page-text">
        Server knowledge, wipe timing, and Rust trivia in a daily format.
      </p>
    </div>
  )
}

function MinigameTodayPanel() {
  return (
    <section className="minigame-today" aria-label="Today's minigame lineup">
      <div className="minigame-today__hero">
        <img src="/rust-minigame-guess-server.png" alt="" />
        <div className="minigame-today__overlay">
          <span className="minigame-option__status">Playable now</span>
          <h3>Start with the daily server puzzle</h3>
          <p>
            Work from a live ranked server pool, use comparison clues, and keep
            guessing until the server clicks.
          </p>
          <div className="minigame-today__actions">
            <Link to="/minigames/guess-the-server" className="button-link">
              Play Guess the Server
            </Link>
            <Link to="/minigames/higher-lower" className="button-link button-link--secondary">
              Play Higher / Lower
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}

function MinigameCardGrid() {
  return (
    <section className="minigame-options" aria-label="Minigame options">
      {gameCards.map((game) => {
        const cardContent = (
          <>
            <div className="minigame-option__media" aria-hidden="true">
              <img src={game.image} alt="" />
              <span className="minigame-option__icon">
                <img src={game.emote} alt="" />
              </span>
            </div>
            <div className="minigame-option__content">
              <div>
                <span className="minigame-option__status">{game.status}</span>
                <h3>{game.title}</h3>
              </div>
              <p>{game.description}</p>
              <span className="minigame-option__meta">{game.meta}</span>
            </div>
          </>
        )
        const className = game.status === 'Playable'
          ? 'minigame-option minigame-option--active'
          : 'minigame-option'

        return (
          <Link
            key={game.title}
            to={game.path}
            className={className}
          >
            {cardContent}
          </Link>
        )
      })}
    </section>
  )
}

function ComingSoonMinigamePage({
  gameTitle,
}: {
  gameTitle: string
}) {
  const game = gameCards.find((item) => item.title === gameTitle)

  if (!game) {
    return null
  }

  return (
    <div className="page-stack minigames-page">
      <Link to="/minigames" className="knowledge-back-link">Back to minigames</Link>
      <section className="minigame-coming-soon">
        <div className="minigame-coming-soon__media" aria-hidden="true">
          <img src={game.image} alt="" />
          <span>
            <img src={game.emote} alt="" />
          </span>
        </div>
        <div className="minigame-coming-soon__copy">
          <p className="page-eyebrow">Minigame</p>
          <h2 className="page-heading">{game.title}</h2>
          <p className="page-text">{game.description}</p>
          <span className="minigame-option__status">{game.status}</span>
        </div>
      </section>
    </div>
  )
}

function MinigameExperience({
  activeGame,
}: {
  activeGame: 'guess' | 'higherLower'
}) {
  const nowMs = useNow()
  const [difficulty, setDifficulty] = useState<GuessDifficulty>('easy')
  const [guessInput, setGuessInput] = useState('')
  const [selectedGuessId, setSelectedGuessId] = useState<string | null>(null)
  const [guessError, setGuessError] = useState<string | null>(null)
  const [isSuggestionsOpen, setIsSuggestionsOpen] = useState(false)
  const [higherLowerMetric, setHigherLowerMetric] = useState<HigherLowerMetric>('averagePlayers')
  const [storedState, setStoredState] = useState<StoredGuessState>(() =>
    normalizeStoredGuessState(readStoredGuessState('easy'), getDailyDateKey(), nowMs, 'easy'))
  const [higherLowerState, setHigherLowerState] = useState<StoredHigherLowerState>(() =>
    normalizeStoredHigherLowerState(readStoredHigherLowerState('averagePlayers'), getDailyDateKey(), nowMs, 'averagePlayers'))
  const [higherLowerReveal, setHigherLowerReveal] = useState<HigherLowerRoundReveal | null>(null)
  const higherLowerRevealTimeoutRef = useRef<number | null>(null)

  const rankedServersQuery = useQuery({
    queryKey: ['minigames', 'ranked-servers'],
    queryFn: async ({ signal }) => {
      const pages = await Promise.all([
        getServers(signal, { page: 1, pageSize: 100, sortBy: 'rank', sortDirection: 'asc' }),
        getServers(signal, { page: 2, pageSize: 100, sortBy: 'rank', sortDirection: 'asc' }),
        getServers(signal, { page: 3, pageSize: 100, sortBy: 'rank', sortDirection: 'asc' }),
      ])

      return pages
        .flatMap((page) => page.items)
        .filter((server) => server.popularityRank !== null && server.popularityRank !== undefined)
        .sort((left, right) => (left.popularityRank ?? Number.MAX_SAFE_INTEGER) - (right.popularityRank ?? Number.MAX_SAFE_INTEGER))
        .slice(0, 250)
    },
    staleTime: 5 * 60 * 1000,
    refetchOnWindowFocus: false,
  })

  const dailyDateKey = getDailyDateKey()
  const selectedDifficulty = difficulties.find((item) => item.value === difficulty) ?? difficulties[0]
  const difficultyStatuses = useMemo(
    () => Object.fromEntries(difficulties.map((item) => [
      item.value,
      getDifficultyStatus(item.value, dailyDateKey, nowMs),
    ])) as Record<GuessDifficulty, DifficultyStatus>,
    [dailyDateKey, nowMs],
  )
  const candidatePool = useMemo(
    () => (rankedServersQuery.data ?? []).slice(0, selectedDifficulty.poolSize),
    [rankedServersQuery.data, selectedDifficulty.poolSize],
  )
  const answer = useMemo(
    () => selectCycleAnswer(candidatePool, getPuzzleKey(storedState, difficulty), difficulty, storedState.previousAnswerId),
    [candidatePool, difficulty, storedState],
  )
  const clueOrder = useMemo(
    () => getClueOrder(getPuzzleKey(storedState, difficulty)),
    [difficulty, storedState],
  )
  const answerHints = useMemo(
    () => answer ? getAnswerHints(answer, selectedDifficulty.poolSize, getPuzzleKey(storedState, difficulty)) : [],
    [answer, selectedDifficulty.poolSize, difficulty, storedState],
  )
  const revealedHints = answerHints.slice(0, storedState.revealedHintCount ?? 0)
  const hasRevealedAllHints = (storedState.revealedHintCount ?? 0) >= progressiveHintCount
  const attemptedServers = useMemo(
    () => storedState.attempts
      .map((serverId) => candidatePool.find((server) => server.id === serverId))
      .filter((server): server is ServerListItem => Boolean(server)),
    [candidatePool, storedState.attempts],
  )
  const isCooldownActive = Boolean(
    storedState.nextAvailableAtUtc && Date.parse(storedState.nextAvailableAtUtc) > nowMs,
  )
  const isPuzzleComplete = Boolean(storedState.completedAtUtc)
  const isGameClosed = isCooldownActive || isPuzzleComplete
  const suggestions = useMemo(
    () => getServerSuggestions(candidatePool, guessInput, storedState.attempts),
    [candidatePool, guessInput, storedState.attempts],
  )
  const higherLowerMetricStatuses = useMemo(
    () => Object.fromEntries(higherLowerMetrics.map((item) => [
      item.value,
      getHigherLowerMetricStatus(item.value, dailyDateKey, nowMs),
    ])) as Record<HigherLowerMetric, DifficultyStatus>,
    [dailyDateKey, nowMs],
  )
  const higherLowerPool = useMemo(
    () => getHigherLowerPool(rankedServersQuery.data ?? [], higherLowerMetric),
    [rankedServersQuery.data, higherLowerMetric],
  )
  const higherLowerPair = useMemo(
    () => selectHigherLowerPair(
      higherLowerPool,
      getHigherLowerPuzzleKey(higherLowerState, higherLowerMetric),
      higherLowerMetric,
      higherLowerState.roundIndex,
    ),
    [higherLowerPool, higherLowerMetric, higherLowerState],
  )
  const higherLowerIsRevealing = Boolean(higherLowerReveal)
  const higherLowerIsCooldownActive = Boolean(
    higherLowerState.nextAvailableAtUtc && Date.parse(higherLowerState.nextAvailableAtUtc) > nowMs,
  )
  const higherLowerIsClosed = higherLowerIsCooldownActive || Boolean(higherLowerState.completedAtUtc)
  const higherLowerHistoryPair = useMemo(() => {
    const latestRound = higherLowerState.history[0]

    if (!latestRound) {
      return null
    }

    const left = higherLowerPool.find((server) => server.id === latestRound.leftId)
    const right = higherLowerPool.find((server) => server.id === latestRound.rightId)

    return left && right ? { left, right } : null
  }, [higherLowerPool, higherLowerState.history])
  const higherLowerDisplayPair = higherLowerReveal
    ? { left: higherLowerReveal.left, right: higherLowerReveal.right }
    : higherLowerIsClosed && higherLowerHistoryPair
      ? higherLowerHistoryPair
      : higherLowerPair
  const higherLowerVisibleStreak = higherLowerReveal ? higherLowerReveal.nextState.streak : higherLowerState.streak
  const higherLowerVisibleBestStreak = higherLowerReveal ? higherLowerReveal.nextState.bestStreak : higherLowerState.bestStreak
  const selectedHigherLowerMetric = higherLowerMetrics.find((item) => item.value === higherLowerMetric) ?? higherLowerMetrics[0]
  const higherLowerRevealTone = higherLowerReveal
    ? higherLowerReveal.result.correct ? 'correct' : 'wrong'
    : null
  const higherLowerRevealDescription = higherLowerReveal
    ? getHigherLowerRevealDescription(higherLowerReveal.result, higherLowerReveal.left, higherLowerReveal.right)
    : ''
  const answerDisplayName = answer ? getServerDisplayName(answer) : 'Unknown'
  const rankedServersErrorDetail =
    rankedServersQuery.error instanceof ApiError
      ? `${rankedServersQuery.error.message} (${rankedServersQuery.error.url})`
      : 'Unexpected error while loading ranked servers.'

  const updateStoredState = useCallback((nextState: StoredGuessState) => {
    setStoredState(nextState)
    window.localStorage.setItem(getStorageKey(difficulty), JSON.stringify(nextState))
  }, [difficulty])

  const updateHigherLowerState = useCallback((nextState: StoredHigherLowerState) => {
    setHigherLowerState(nextState)
    window.localStorage.setItem(getHigherLowerStorageKey(higherLowerMetric), JSON.stringify(nextState))
  }, [higherLowerMetric])

  const clearHigherLowerRevealTimer = useCallback(() => {
    if (higherLowerRevealTimeoutRef.current !== null) {
      window.clearTimeout(higherLowerRevealTimeoutRef.current)
      higherLowerRevealTimeoutRef.current = null
    }
  }, [])

  useEffect(() => {
    const nextState = normalizeStoredGuessState(readStoredGuessState(difficulty), dailyDateKey, Date.now(), difficulty)
    setStoredState(nextState)
    setGuessInput('')
    setSelectedGuessId(null)
    setGuessError(null)
    setIsSuggestionsOpen(false)
  }, [difficulty, dailyDateKey])

  useEffect(() => {
    clearHigherLowerRevealTimer()
    setHigherLowerReveal(null)
    const nextState = normalizeStoredHigherLowerState(readStoredHigherLowerState(higherLowerMetric), dailyDateKey, Date.now(), higherLowerMetric)
    setHigherLowerState(nextState)
  }, [clearHigherLowerRevealTimer, higherLowerMetric, dailyDateKey])

  useEffect(() => () => {
    clearHigherLowerRevealTimer()
  }, [clearHigherLowerRevealTimer])

  useEffect(() => {
    const nextAvailableMs = storedState.nextAvailableAtUtc ? Date.parse(storedState.nextAvailableAtUtc) : null
    if (storedState.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
      updateStoredState(createFreshGuessState(difficulty, dailyDateKey, storedState.answerId ?? storedState.previousAnswerId))
      setGuessInput('')
      setSelectedGuessId(null)
      setGuessError(null)
      setIsSuggestionsOpen(false)
    }
  }, [dailyDateKey, difficulty, nowMs, storedState, updateStoredState])

  useEffect(() => {
    const nextAvailableMs = higherLowerState.nextAvailableAtUtc ? Date.parse(higherLowerState.nextAvailableAtUtc) : null
    if (higherLowerState.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
      updateHigherLowerState(createFreshHigherLowerState(higherLowerMetric, dailyDateKey, higherLowerState.bestStreak))
    }
  }, [dailyDateKey, higherLowerMetric, higherLowerState, nowMs, updateHigherLowerState])

  function submitGuess(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!answer || isGameClosed) {
      return
    }

    const guessedServer = resolveGuessedServer(candidatePool, guessInput, selectedGuessId)

    if (!guessedServer) {
      setGuessError('Choose a server from the dropdown.')
      return
    }

    if (storedState.attempts.includes(guessedServer.id)) {
      setGuessError('You already tried that server.')
      return
    }

    const isRepeatGuess = storedState.attempts.includes(guessedServer.id)
    const nextAttempts = isRepeatGuess ? storedState.attempts : [...storedState.attempts, guessedServer.id]
    const isCorrect = guessedServer.id === answer.id
    const nextRevealedHintCount = isCorrect || isRepeatGuess
      ? storedState.revealedHintCount ?? 0
      : Math.min((storedState.revealedHintCount ?? 0) + 1, progressiveHintCount)
    const baseState: StoredGuessState = {
      dateKey: dailyDateKey,
      puzzleKey: getPuzzleKey(storedState, difficulty),
      previousAnswerId: storedState.previousAnswerId,
      attempts: nextAttempts,
      revealedHintCount: nextRevealedHintCount,
    }

    if (isCorrect) {
      const completedAtUtc = new Date().toISOString()
      updateStoredState({
        ...baseState,
        solved: isCorrect,
        completedAtUtc,
        nextAvailableAtUtc: new Date(Date.now() + cooldownMs).toISOString(),
        answerId: answer.id,
      })
    } else {
      updateStoredState(baseState)
    }

    setGuessInput('')
    setSelectedGuessId(null)
    setGuessError(null)
    setIsSuggestionsOpen(false)
  }

  function revealAnswer() {
    if (!answer || !hasRevealedAllHints || isGameClosed) {
      return
    }

    const completedAtUtc = new Date().toISOString()
    updateStoredState({
      ...storedState,
      dateKey: dailyDateKey,
      puzzleKey: getPuzzleKey(storedState, difficulty),
      revealedHintCount: progressiveHintCount,
      solved: false,
      revealedAnswer: true,
      completedAtUtc,
      nextAvailableAtUtc: new Date(Date.now() + cooldownMs).toISOString(),
      answerId: answer.id,
    })
  }

  function selectSuggestion(server: ServerListItem) {
    setGuessInput(getServerDisplayName(server))
    setSelectedGuessId(server.id)
    setGuessError(null)
    setIsSuggestionsOpen(false)
  }

  function submitHigherLowerChoice(choice: HigherLowerChoice) {
    if (!higherLowerPair || higherLowerIsClosed || higherLowerIsRevealing) {
      return
    }

    const leftValue = getHigherLowerMetricValue(higherLowerPair.left, higherLowerMetric)
    const rightValue = getHigherLowerMetricValue(higherLowerPair.right, higherLowerMetric)

    if (leftValue === null || rightValue === null) {
      return
    }

    const correctChoice = getHigherLowerCorrectChoice(leftValue, rightValue, higherLowerMetric)
    const isCorrect = choice === correctChoice
    const nextStreak = isCorrect ? higherLowerState.streak + 1 : 0
    const nextBestStreak = Math.max(higherLowerState.bestStreak, nextStreak)
    const nextHistory: HigherLowerRoundResult[] = [
      {
        leftId: higherLowerPair.left.id,
        rightId: higherLowerPair.right.id,
        choice,
        correct: isCorrect,
        metric: higherLowerMetric,
        leftValue,
        rightValue,
      },
      ...higherLowerState.history,
    ].slice(0, 8)
    const completedAtUtc = !isCorrect || nextStreak >= higherLowerWinStreak
      ? new Date().toISOString()
      : undefined
    const nextState = {
      ...higherLowerState,
      dateKey: dailyDateKey,
      puzzleKey: getHigherLowerPuzzleKey(higherLowerState, higherLowerMetric),
      roundIndex: completedAtUtc ? higherLowerState.roundIndex : higherLowerState.roundIndex + 1,
      streak: nextStreak,
      bestStreak: nextBestStreak,
      history: nextHistory,
      solved: isCorrect && nextStreak >= higherLowerWinStreak,
      completedAtUtc,
      nextAvailableAtUtc: completedAtUtc ? new Date(Date.now() + cooldownMs).toISOString() : undefined,
    }
    const result = nextHistory[0]

    setHigherLowerReveal({
      left: higherLowerPair.left,
      right: higherLowerPair.right,
      result,
      nextState,
    })

    clearHigherLowerRevealTimer()
    higherLowerRevealTimeoutRef.current = window.setTimeout(() => {
      updateHigherLowerState(nextState)
      setHigherLowerReveal(null)
      higherLowerRevealTimeoutRef.current = null
    }, higherLowerRevealDelayMs)
  }

  return (
    <div className="page-stack minigames-page">
      <Link to="/minigames" className="knowledge-back-link">Back to minigames</Link>

      {activeGame === 'guess' ? (
        <SectionCard
          title="Guess the Server"
          description="The answer is selected from the RustIQ Top 50, Top 100, or Top 250. Each difficulty has its own 24 hour cooldown."
        >
        <div className="difficulty-selector" aria-label="Guess the Server difficulty">
          {difficulties.map((item) => (
            (() => {
              const status = difficultyStatuses[item.value]

              return (
                <button
                  key={item.value}
                  type="button"
                  className={item.value === difficulty ? 'difficulty-button difficulty-button--active' : 'difficulty-button'}
                  onClick={() => setDifficulty(item.value)}
                >
                  <span>{item.label}</span>
                  <small>Top {item.poolSize}</small>
                  <strong className={status.isLocked ? 'difficulty-button__timer difficulty-button__timer--locked' : 'difficulty-button__timer'}>
                    {status.label}
                  </strong>
                </button>
              )
            })()
          ))}
        </div>

        {rankedServersQuery.isLoading ? (
          <StateNotice
            title="Loading ranked servers"
            description="Reading the stored Top 250 ranking from the server list API."
          />
        ) : null}

        {rankedServersQuery.isError ? (
          <StateNotice
            title="Could not load ranked servers"
            description="The game needs the RustIQ ranking pool before it can start."
            detail={rankedServersErrorDetail}
            tone="error"
          />
        ) : null}

        {!rankedServersQuery.isLoading && !rankedServersQuery.isError && candidatePool.length < selectedDifficulty.poolSize ? (
          <StateNotice
            title="Ranking pool is still warming up"
            description={`This difficulty needs ${selectedDifficulty.poolSize} ranked servers. Current ranked pool: ${candidatePool.length}.`}
            tone="warning"
          />
        ) : null}

        {answer && candidatePool.length >= selectedDifficulty.poolSize ? (
          <div className="guess-server-game">
            <div className="guess-server-game__status">
              <div>
                <span className="minigame-option__status">{selectedDifficulty.label}</span>
                <h3>Daily Top {selectedDifficulty.poolSize} puzzle</h3>
                <p className="page-text">
                  {selectedDifficulty.poolSize} possible servers. Guesses do not run out.
                </p>
              </div>
              <div className="guess-server-game__stats" aria-label="Puzzle status">
                <div>
                  <span>Attempts</span>
                  <strong>{storedState.attempts.length}</strong>
                </div>
                <div>
                  <span>Hints</span>
                  <strong>{1 + (storedState.revealedHintCount ?? 0)}/{1 + progressiveHintCount}</strong>
                </div>
              </div>
              <div className="guess-server-game__visual" aria-hidden="true">
                <img src="/rust-minigame-guess-server.png" alt="" />
                <div>
                  <span>Signal</span>
                  <strong>Live rank feed</strong>
                </div>
              </div>
            </div>

            <div className="guess-server-hint" aria-label="Starting hint">
              <img className="guess-server-hint__emote" src="/rust-minigame-emote-hint.png" alt="" />
              <div>
                <span className="guess-server-hint__eyebrow">Starting hint</span>
                <h4>{getStartingHint(answer)}</h4>
              </div>
            </div>

            {revealedHints.length > 0 ? (
              <div className="guess-server-revealed-hints" aria-label="Revealed hints">
                {revealedHints.map((hint, index) => (
                  <div key={hint.label} className="guess-server-revealed-hint">
                    <span>Hint {index + 2}</span>
                    <strong>{hint.label}</strong>
                    <p>{hint.value}</p>
                  </div>
                ))}
              </div>
            ) : null}

            {isGameClosed ? (
              <StateNotice
                title={storedState.solved ? 'Solved' : storedState.revealedAnswer ? 'Answer revealed' : 'Cooldown active'}
                description={
                  <span className="guess-server-answer-notice">
                    <span>{storedState.revealedAnswer ? 'Revealed answer' : 'Answer'}</span>
                    <strong>{answerDisplayName}</strong>
                  </span>
                }
                tone={storedState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="guess-server-actions">
                <form className="guess-server-form" onSubmit={submitGuess}>
                  <label className="guess-server-input">
                    <span>Your guess</span>
                    <input
                      type="search"
                      value={guessInput}
                      onChange={(event) => {
                        setGuessInput(event.target.value)
                        setSelectedGuessId(null)
                        setGuessError(null)
                        setIsSuggestionsOpen(true)
                      }}
                      onFocus={() => setIsSuggestionsOpen(true)}
                      onBlur={() => window.setTimeout(() => setIsSuggestionsOpen(false), 120)}
                      placeholder="Start typing a ranked server name"
                      autoComplete="off"
                    />
                    {isSuggestionsOpen && suggestions.length > 0 ? (
                      <div className="guess-server-suggestions" role="listbox">
                        {suggestions.map((server) => (
                          <button
                            key={server.id}
                            type="button"
                            className="guess-server-suggestion"
                            onMouseDown={(event) => event.preventDefault()}
                            onClick={() => selectSuggestion(server)}
                          >
                            <span>#{server.popularityRank} {getServerDisplayName(server)}</span>
                            <small>{formatCountry(server.country)} | {getRustServerTypeLabel(server.serverType)}</small>
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </label>

                  <button type="submit" className="button-link">
                    Guess
                  </button>
                </form>

                {hasRevealedAllHints ? (
                  <button
                    type="button"
                    className="button-link button-link--secondary"
                    onClick={revealAnswer}
                  >
                    Reveal server
                  </button>
                ) : null}
              </div>
            )}

            {guessError ? (
              <p className="guess-server-error">{guessError}</p>
            ) : null}

            <div className="guess-server-clues" aria-label="Guess history">
              {attemptedServers.length === 0 ? (
                <p className="page-text">Each wrong guess reveals one more hint. After all hints are revealed, you can keep guessing or reveal the server.</p>
              ) : (
                attemptedServers.map((server, index) => (
                  <GuessResultRow
                    key={server.id}
                    guess={server}
                    answer={answer}
                    attemptNumber={index + 1}
                    nowMs={nowMs}
                    clueOrder={clueOrder}
                  />
                ))
              )}
            </div>
          </div>
        ) : null}
        </SectionCard>
      ) : null}

      {activeGame === 'higherLower' ? (
        <SectionCard
          title="Higher / Lower"
          description="Pick whether the challenger is higher or lower than the anchor server. Reach a streak of 10 before one wrong answer."
        >
        <div className="higher-lower-modes" aria-label="Higher Lower metric">
          {higherLowerMetrics.map((item) => {
            const status = higherLowerMetricStatuses[item.value]

            return (
              <button
                key={item.value}
                type="button"
                className={item.value === higherLowerMetric ? 'higher-lower-mode higher-lower-mode--active' : 'higher-lower-mode'}
                disabled={higherLowerIsRevealing}
                onClick={() => setHigherLowerMetric(item.value)}
              >
                <span>{item.label}</span>
                <small>{item.detail}</small>
                <strong className={status.isLocked ? 'difficulty-button__timer difficulty-button__timer--locked' : 'difficulty-button__timer'}>
                  {status.label}
                </strong>
              </button>
            )
          })}
        </div>

        {rankedServersQuery.isLoading ? (
          <StateNotice
            title="Loading comparison pool"
            description="Higher / Lower uses the same ranked server data as the daily server puzzle."
          />
        ) : null}

        {rankedServersQuery.isError ? (
          <StateNotice
            title="Could not load comparison pool"
            description="The game needs ranked servers before it can build matchups."
            detail={rankedServersErrorDetail}
            tone="error"
          />
        ) : null}

        {!rankedServersQuery.isLoading && !rankedServersQuery.isError && higherLowerPool.length < 12 ? (
          <StateNotice
            title="Comparison pool is still warming up"
            description={`This mode needs at least 12 servers with ${selectedHigherLowerMetric.label.toLowerCase()} data. Current pool: ${higherLowerPool.length}.`}
            tone="warning"
          />
        ) : null}

        {higherLowerDisplayPair && higherLowerPool.length >= 12 ? (
          <div className={higherLowerRevealTone ? `higher-lower-game higher-lower-game--${higherLowerRevealTone}` : 'higher-lower-game'}>
            <div className="higher-lower-header">
              <div>
                <span className="minigame-option__status">{selectedHigherLowerMetric.label}</span>
                <h3>Round {higherLowerState.roundIndex + 1}</h3>
                <p className="page-text">
                  Compare the hidden challenger against the revealed anchor server.
                </p>
              </div>
              <div className="guess-server-game__stats" aria-label="Higher Lower status">
                <div>
                  <span>Streak</span>
                  <strong>{higherLowerVisibleStreak}/{higherLowerWinStreak}</strong>
                </div>
                <div>
                  <span>Best</span>
                  <strong>{higherLowerVisibleBestStreak}</strong>
                </div>
              </div>
            </div>

            <div className="higher-lower-board">
              <HigherLowerServerCard
                server={higherLowerDisplayPair.left}
                metric={higherLowerMetric}
                label="Anchor"
                isValueHidden={false}
              />

              <div className="higher-lower-versus" aria-hidden="true">
                <img src="/rust-minigame-emote-streak.png" alt="" />
                <span>VS</span>
              </div>

              <HigherLowerServerCard
                server={higherLowerDisplayPair.right}
                metric={higherLowerMetric}
                label="Challenger"
                isValueHidden={!higherLowerIsClosed && !higherLowerIsRevealing}
                resultTone={higherLowerRevealTone ?? undefined}
              />
            </div>

            {higherLowerReveal ? (
              <div
                className={`higher-lower-feedback higher-lower-feedback--${higherLowerRevealTone}`}
                role="status"
                aria-live="polite"
              >
                <img
                  src={higherLowerReveal.result.correct ? '/rust-minigame-emote-correct.png' : '/rust-minigame-emote-wrong.png'}
                  alt=""
                />
                <div>
                  <strong>{higherLowerReveal.result.correct ? 'Correct' : 'Incorrect'}</strong>
                  <span>{higherLowerRevealDescription}</span>
                </div>
              </div>
            ) : higherLowerIsClosed ? (
              <StateNotice
                title={higherLowerState.solved ? 'Streak cleared' : 'Run ended'}
                description={
                  higherLowerState.solved
                    ? `You cleared ${higherLowerWinStreak} in a row.`
                    : `Best streak: ${higherLowerState.bestStreak}.`
                }
                tone={higherLowerState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="higher-lower-actions" aria-label="Choose challenger comparison">
                <button
                  type="button"
                  className="higher-lower-choice higher-lower-choice--higher"
                  onClick={() => submitHigherLowerChoice('higher')}
                >
                  <span className="higher-lower-choice__arrow" aria-hidden="true">↑</span>
                  <span>
                    <strong>Higher</strong>
                    <small>Challenger is above the anchor</small>
                  </span>
                </button>
                <button
                  type="button"
                  className="higher-lower-choice higher-lower-choice--lower"
                  onClick={() => submitHigherLowerChoice('lower')}
                >
                  <span className="higher-lower-choice__arrow" aria-hidden="true">↓</span>
                  <span>
                    <strong>Lower</strong>
                    <small>Challenger is below the anchor</small>
                  </span>
                </button>
              </div>
            )}

            <div className="higher-lower-history" aria-label="Higher Lower history">
              {higherLowerState.history.length === 0 ? (
                <p className="page-text">Your last comparisons will appear here after each choice.</p>
              ) : (
                higherLowerState.history.map((round, index) => (
                  <HigherLowerHistoryRow
                    key={`${round.leftId}:${round.rightId}:${index}`}
                    round={round}
                    servers={higherLowerPool}
                  />
                ))
              )}
            </div>
          </div>
        ) : null}
        </SectionCard>
      ) : null}
    </div>
  )
}

function HigherLowerServerCard({
  server,
  metric,
  label,
  isValueHidden,
  resultTone,
}: {
  server: ServerListItem
  metric: HigherLowerMetric
  label: string
  isValueHidden: boolean
  resultTone?: 'correct' | 'wrong'
}) {
  const metricValue = getHigherLowerMetricValue(server, metric)
  const cardClassName = [
    'higher-lower-server',
    resultTone ? `higher-lower-server--${resultTone}` : null,
  ].filter(Boolean).join(' ')
  const valueClassName = [
    'higher-lower-server__value',
    isValueHidden ? 'higher-lower-server__value--hidden' : null,
    resultTone ? 'higher-lower-server__value--revealed' : null,
    resultTone ? `higher-lower-server__value--${resultTone}` : null,
  ].filter(Boolean).join(' ')

  return (
    <article className={cardClassName}>
      <div className="higher-lower-server__topline">
        <span>{label}</span>
        {server.popularityRank ? <strong>#{server.popularityRank}</strong> : null}
      </div>
      <h3>{getServerDisplayName(server)}</h3>
      <div className={valueClassName}>
        <span>{getHigherLowerMetricLabel(metric)}</span>
        <strong>{isValueHidden ? '???' : formatHigherLowerMetricValue(metricValue, metric)}</strong>
      </div>
      <div className="higher-lower-server__meta">
        <span>{formatCountry(server.country)}</span>
        <span>{getRustServerTypeLabel(server.serverType)}</span>
        <span>{server.maxPlayers} max</span>
      </div>
    </article>
  )
}

function HigherLowerHistoryRow({
  round,
  servers,
}: {
  round: HigherLowerRoundResult
  servers: ServerListItem[]
}) {
  const left = servers.find((server) => server.id === round.leftId)
  const right = servers.find((server) => server.id === round.rightId)
  const leftName = left ? getServerDisplayName(left) : 'Previous anchor'
  const rightName = right ? getServerDisplayName(right) : 'Previous challenger'

  return (
    <article className={round.correct ? 'higher-lower-history-row higher-lower-history-row--correct' : 'higher-lower-history-row'}>
      <img
        src={round.correct ? '/rust-minigame-emote-correct.png' : '/rust-minigame-emote-wrong.png'}
        alt=""
      />
      <div>
        <strong>{rightName}</strong>
        <p>
          {round.choice === 'higher' ? 'Higher' : 'Lower'} than {leftName} on {getHigherLowerMetricLabel(round.metric).toLowerCase()}.
        </p>
      </div>
      <span>
        {formatHigherLowerMetricValue(round.leftValue, round.metric)} / {formatHigherLowerMetricValue(round.rightValue, round.metric)}
      </span>
    </article>
  )
}

function getHigherLowerRevealDescription(
  round: HigherLowerRoundResult,
  left: ServerListItem,
  right: ServerListItem,
) {
  return `${getServerDisplayName(right)} was ${formatHigherLowerMetricValue(round.rightValue, round.metric)}. ${getServerDisplayName(left)} was ${formatHigherLowerMetricValue(round.leftValue, round.metric)}.`
}

function GuessResultRow({
  guess,
  answer,
  attemptNumber,
  nowMs,
  clueOrder,
}: {
  guess: ServerListItem
  answer: ServerListItem
  attemptNumber: number
  nowMs: number
  clueOrder: ClueKind[]
}) {
  const isCorrect = guess.id === answer.id
  const clues = buildGuessClues(guess, answer, nowMs)

  return (
    <article className={isCorrect ? 'guess-result guess-result--correct' : 'guess-result guess-result--wrong'}>
      <div className="guess-result__header">
        <span>#{attemptNumber}</span>
        <img
          className="guess-result__emote"
          src={isCorrect ? '/rust-minigame-emote-correct.png' : '/rust-minigame-emote-wrong.png'}
          alt=""
        />
        <strong>{getServerDisplayName(guess)}</strong>
      </div>
      <div className="guess-result__clues">
        {clueOrder.map((clueKind) => {
          const clue = clues[clueKind]

          return (
            <Clue
              key={clueKind}
              label={clue.label}
              value={clue.value}
              tone={clue.tone}
            />
          )
        })}
      </div>
    </article>
  )
}

function Clue({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone?: 'correct' | 'wrong' | 'neutral'
}) {
  return (
    <div className={`guess-clue guess-clue--${tone ?? 'neutral'}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function getStorageKey(difficulty: GuessDifficulty) {
  return `rustiq-minigames.guess-server.${difficulty}`
}

function readStoredGuessState(difficulty: GuessDifficulty): StoredGuessState | null {
  try {
    const rawState = window.localStorage.getItem(getStorageKey(difficulty))
    return rawState ? JSON.parse(rawState) as StoredGuessState : null
  } catch {
    return null
  }
}

function getHigherLowerStorageKey(metric: HigherLowerMetric) {
  return `rustiq-minigames.higher-lower.${metric}`
}

function readStoredHigherLowerState(metric: HigherLowerMetric): StoredHigherLowerState | null {
  try {
    const rawState = window.localStorage.getItem(getHigherLowerStorageKey(metric))
    return rawState ? JSON.parse(rawState) as StoredHigherLowerState : null
  } catch {
    return null
  }
}

function getDifficultyStatus(
  difficulty: GuessDifficulty,
  dateKey: string,
  nowMs: number,
): DifficultyStatus {
  const state = normalizeStoredGuessState(readStoredGuessState(difficulty), dateKey, nowMs, difficulty)
  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  const isLocked = Boolean(state.completedAtUtc && nextAvailableMs && nextAvailableMs > nowMs)

  return {
    isLocked,
    label: isLocked ? formatCooldown(state.nextAvailableAtUtc, nowMs) : 'Available',
  }
}

function getHigherLowerMetricStatus(
  metric: HigherLowerMetric,
  dateKey: string,
  nowMs: number,
): DifficultyStatus {
  const state = normalizeStoredHigherLowerState(readStoredHigherLowerState(metric), dateKey, nowMs, metric)
  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  const isLocked = Boolean(state.completedAtUtc && nextAvailableMs && nextAvailableMs > nowMs)

  return {
    isLocked,
    label: isLocked ? formatCooldown(state.nextAvailableAtUtc, nowMs) : 'Available',
  }
}

function normalizeStoredGuessState(
  state: StoredGuessState | null,
  dateKey: string,
  nowMs: number,
  difficulty: GuessDifficulty,
): StoredGuessState {
  if (!state) {
    return createFreshGuessState(difficulty, dateKey)
  }

  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  if (state.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
    return createFreshGuessState(difficulty, dateKey, state.answerId ?? state.previousAnswerId)
  }

  return {
    ...state,
    dateKey: state.dateKey || dateKey,
    puzzleKey: state.puzzleKey ?? createPuzzleKey(difficulty, Date.parse(state.completedAtUtc ?? '') || nowMs),
    attempts: Array.isArray(state.attempts) ? state.attempts : [],
    revealedHintCount: Math.min(Math.max(state.revealedHintCount ?? 0, 0), progressiveHintCount),
  }
}

function normalizeStoredHigherLowerState(
  state: StoredHigherLowerState | null,
  dateKey: string,
  nowMs: number,
  metric: HigherLowerMetric,
): StoredHigherLowerState {
  if (!state) {
    return createFreshHigherLowerState(metric, dateKey)
  }

  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  if (state.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
    return createFreshHigherLowerState(metric, dateKey, state.bestStreak)
  }

  return {
    ...state,
    dateKey: state.dateKey || dateKey,
    puzzleKey: state.puzzleKey ?? createHigherLowerPuzzleKey(metric),
    roundIndex: Math.max(state.roundIndex ?? 0, 0),
    streak: Math.max(state.streak ?? 0, 0),
    bestStreak: Math.max(state.bestStreak ?? 0, state.streak ?? 0, 0),
    history: Array.isArray(state.history) ? state.history.slice(0, 8) : [],
  }
}

function createFreshGuessState(
  difficulty: GuessDifficulty,
  dateKey: string,
  previousAnswerId?: string,
): StoredGuessState {
  return {
    dateKey,
    puzzleKey: createPuzzleKey(difficulty),
    previousAnswerId,
    revealedHintCount: 0,
    attempts: [],
  }
}

function createFreshHigherLowerState(
  metric: HigherLowerMetric,
  dateKey: string,
  bestStreak = 0,
): StoredHigherLowerState {
  return {
    dateKey,
    puzzleKey: createHigherLowerPuzzleKey(metric),
    roundIndex: 0,
    streak: 0,
    bestStreak,
    history: [],
  }
}

function getPuzzleKey(state: StoredGuessState, difficulty: GuessDifficulty) {
  return state.puzzleKey ?? `${state.dateKey}:${difficulty}:legacy`
}

function getHigherLowerPuzzleKey(state: StoredHigherLowerState, metric: HigherLowerMetric) {
  return state.puzzleKey ?? `${state.dateKey}:${metric}:higher-lower:legacy`
}

function createPuzzleKey(difficulty: GuessDifficulty, timestampMs = Date.now()) {
  return `${difficulty}:${timestampMs}`
}

function createHigherLowerPuzzleKey(metric: HigherLowerMetric, timestampMs = Date.now()) {
  return `${metric}:${timestampMs}`
}

function getDailyDateKey() {
  return new Date().toISOString().slice(0, 10)
}

function selectCycleAnswer(
  candidatePool: ServerListItem[],
  puzzleKey: string,
  difficulty: GuessDifficulty,
  previousAnswerId: string | undefined,
) {
  if (candidatePool.length === 0) {
    return null
  }

  const answerIndex = hashString(`${puzzleKey}:${difficulty}:guess-server`) % candidatePool.length
  const answer = candidatePool[answerIndex]

  if (candidatePool.length > 1 && answer.id === previousAnswerId) {
    return candidatePool[(answerIndex + 1) % candidatePool.length]
  }

  return answer
}

function getHigherLowerPool(servers: ServerListItem[], metric: HigherLowerMetric) {
  return servers
    .filter((server) => getHigherLowerMetricValue(server, metric) !== null)
    .slice(0, 250)
}

function selectHigherLowerPair(
  pool: ServerListItem[],
  puzzleKey: string,
  metric: HigherLowerMetric,
  roundIndex: number,
) {
  if (pool.length < 2) {
    return null
  }

  const seed = hashString(`${puzzleKey}:${metric}:${roundIndex}:higher-lower`)
  const leftIndex = seed % pool.length
  const left = pool[leftIndex]
  const leftValue = getHigherLowerMetricValue(left, metric)

  for (let offset = 1; offset < pool.length; offset += 1) {
    const rightIndex = (leftIndex + offset + (seed % 17)) % pool.length
    const right = pool[rightIndex]
    const rightValue = getHigherLowerMetricValue(right, metric)

    if (right.id !== left.id && rightValue !== null && leftValue !== null && rightValue !== leftValue) {
      return { left, right }
    }
  }

  return null
}

function getHigherLowerMetricValue(server: ServerListItem, metric: HigherLowerMetric) {
  switch (metric) {
    case 'averagePlayers':
      return server.rankedAveragePlayers ?? null
    case 'currentPlayers':
      return server.players
    case 'rank':
      return server.popularityRank ?? null
  }
}

function getHigherLowerCorrectChoice(
  leftValue: number,
  rightValue: number,
  metric: HigherLowerMetric,
): HigherLowerChoice {
  if (metric === 'rank') {
    return rightValue < leftValue ? 'higher' : 'lower'
  }

  return rightValue > leftValue ? 'higher' : 'lower'
}

function getHigherLowerMetricLabel(metric: HigherLowerMetric) {
  switch (metric) {
    case 'averagePlayers':
      return 'Average pop'
    case 'currentPlayers':
      return 'Live players'
    case 'rank':
      return 'Rank'
  }
}

function formatHigherLowerMetricValue(value: number | null, metric: HigherLowerMetric) {
  if (value === null) {
    return 'Unknown'
  }

  if (metric === 'rank') {
    return `#${Math.round(value)}`
  }

  return Math.round(value).toLocaleString()
}

function hashString(value: string) {
  let hash = 2166136261
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index)
    hash = Math.imul(hash, 16777619)
  }

  return Math.abs(hash >>> 0)
}

function getServerSuggestions(
  candidatePool: ServerListItem[],
  input: string,
  attemptedServerIds: string[],
) {
  const normalizedInput = normalizeGuess(input)
  const availableServers = candidatePool.filter((server) => !attemptedServerIds.includes(server.id))

  if (!normalizedInput) {
    return availableServers
  }

  return availableServers
    .filter((server) => normalizeGuess(getServerDisplayName(server)).includes(normalizedInput))
}

function resolveGuessedServer(
  candidatePool: ServerListItem[],
  input: string,
  selectedGuessId: string | null,
) {
  if (selectedGuessId) {
    return candidatePool.find((server) => server.id === selectedGuessId) ?? null
  }

  const normalizedInput = normalizeGuess(input)
  return candidatePool.find((server) => normalizeGuess(getServerDisplayName(server)) === normalizedInput) ?? null
}

function normalizeGuess(value: string) {
  return value.trim().toLowerCase().replace(/\s+/g, ' ')
}

function compareRank(guess: ServerListItem, answer: ServerListItem) {
  if (guess.popularityRank === answer.popularityRank) {
    return 'Correct'
  }

  if (!guess.popularityRank || !answer.popularityRank) {
    return 'Unknown'
  }

  return answer.popularityRank < guess.popularityRank ? 'Answer ranks higher' : 'Answer ranks lower'
}

function getStartingHint(answer: ServerListItem) {
  const averagePlayers = answer.rankedAveragePlayers

  if (averagePlayers !== null && averagePlayers !== undefined) {
    const populationFloor = Math.floor(averagePlayers / 50) * 50
    const populationCeiling = populationFloor + 49

    return `Average population is around ${populationFloor}-${populationCeiling}.`
  }

  return `Average population data is still warming up for this server.`
}

function getAnswerHints(
  answer: ServerListItem,
  poolSize: number,
  puzzleKey: string,
): GuessClue[] {
  const shuffledHints = shuffleWithSeed<GuessClue>([
    {
      label: 'Region',
      value: `The server region is ${formatCountry(answer.country)}.`,
    },
    {
      label: 'Server type',
      value: `The server type is ${getRustServerTypeLabel(answer.serverType)}.`,
    },
    {
      label: 'Capacity',
      value: `The max player count is ${answer.maxPlayers}.`,
    },
    {
      label: 'Wipe timing',
      value: getWipeHint(answer.lastWipeAtUtc),
    },
  ], hashString(`${puzzleKey}:answer-hints`))

  return [
    ...shuffledHints.slice(0, progressiveHintCount - 1),
    {
      label: 'Rank range',
      value: getRankRangeHint(answer, poolSize),
    },
  ]
}

function getRankRangeHint(answer: ServerListItem, poolSize: number) {
  const rank = answer.popularityRank ?? poolSize
  const rankBucketSize = poolSize <= 50 ? 10 : poolSize <= 100 ? 20 : 50
  const rankFloor = Math.floor((rank - 1) / rankBucketSize) * rankBucketSize + 1
  const rankCeiling = Math.min(rankFloor + rankBucketSize - 1, poolSize)

  return `The answer is ranked between #${rankFloor} and #${rankCeiling}.`
}

function getWipeHint(lastWipeAtUtc: string | null) {
  if (!lastWipeAtUtc) {
    return 'Wipe timing is unknown for this server.'
  }

  const parsedWipe = Date.parse(lastWipeAtUtc)
  if (Number.isNaN(parsedWipe)) {
    return 'Wipe timing is unknown for this server.'
  }

  const ageDays = Math.max(0, Math.round((Date.now() - parsedWipe) / (24 * 60 * 60 * 1000)))

  if (ageDays === 0) {
    return 'The server wiped today.'
  }

  if (ageDays === 1) {
    return 'The server wiped about 1 day ago.'
  }

  return `The server wiped about ${ageDays} days ago.`
}

function getClueOrder(puzzleKey: string): ClueKind[] {
  const shuffledClues = shuffleWithSeed<ClueKind>(
    ['region', 'type', 'players', 'maxPlayers', 'wipe'],
    hashString(`${puzzleKey}:clue-order`),
  )

  return [...shuffledClues, 'rank']
}

function buildGuessClues(
  guess: ServerListItem,
  answer: ServerListItem,
  nowMs: number,
): Record<ClueKind, GuessClue> {
  const rankComparison = compareRank(guess, answer)
  const playersComparison = compareNumber(guess.rankedAveragePlayers, answer.rankedAveragePlayers, 'Avg pop')
  const maxPlayersComparison = compareNumber(guess.maxPlayers, answer.maxPlayers, 'Max')
  const wipeComparison = compareWipe(guess.lastWipeAtUtc, answer.lastWipeAtUtc, nowMs)

  return {
    region: {
      label: 'Region',
      value: formatCountry(guess.country),
      tone: guess.country === answer.country ? 'correct' : 'wrong',
    },
    type: {
      label: 'Type',
      value: getRustServerTypeLabel(guess.serverType),
      tone: guess.serverType === answer.serverType ? 'correct' : 'wrong',
    },
    players: {
      label: 'Players',
      value: playersComparison,
      tone: getTone(playersComparison, 'Avg pop: correct'),
    },
    maxPlayers: {
      label: 'Max',
      value: maxPlayersComparison,
      tone: getTone(maxPlayersComparison, 'Max: correct'),
    },
    wipe: {
      label: 'Wipe',
      value: wipeComparison,
      tone: getTone(wipeComparison, 'Same day'),
    },
    rank: {
      label: 'Rank',
      value: rankComparison,
      tone: getTone(rankComparison, 'Correct'),
    },
  }
}

function shuffleWithSeed<T>(items: T[], seed: number) {
  const shuffledItems = [...items]
  let currentSeed = seed

  for (let index = shuffledItems.length - 1; index > 0; index -= 1) {
    currentSeed = Math.imul(currentSeed ^ 0x9e3779b9, 1664525) + 1013904223
    const swapIndex = Math.abs(currentSeed) % (index + 1)
    const currentItem = shuffledItems[index]
    shuffledItems[index] = shuffledItems[swapIndex]
    shuffledItems[swapIndex] = currentItem
  }

  return shuffledItems
}

function compareNumber(
  guessValue: number | null | undefined,
  answerValue: number | null | undefined,
  label: string,
) {
  if (guessValue === null || guessValue === undefined || answerValue === null || answerValue === undefined) {
    return 'Unknown'
  }

  if (Math.round(guessValue) === Math.round(answerValue)) {
    return `${label}: correct`
  }

  return answerValue > guessValue ? `${label}: higher` : `${label}: lower`
}

function compareWipe(
  guessWipeAtUtc: string | null,
  answerWipeAtUtc: string | null,
  nowMs: number,
) {
  if (!guessWipeAtUtc || !answerWipeAtUtc) {
    return 'Unknown'
  }

  const guessMs = Date.parse(guessWipeAtUtc)
  const answerMs = Date.parse(answerWipeAtUtc)

  if (Number.isNaN(guessMs) || Number.isNaN(answerMs)) {
    return 'Unknown'
  }

  const dayMs = 24 * 60 * 60 * 1000
  if (Math.abs(guessMs - answerMs) < dayMs) {
    return 'Same day'
  }

  const answerAgeDays = Math.round((nowMs - answerMs) / dayMs)
  return answerMs > guessMs ? `Newer (${answerAgeDays}d)` : `Older (${answerAgeDays}d)`
}

function getTone(value: string, correctValue: string): 'correct' | 'wrong' | 'neutral' {
  if (value === correctValue) {
    return 'correct'
  }

  if (value === 'Unknown') {
    return 'neutral'
  }

  return 'wrong'
}

function formatCooldown(nextAvailableAtUtc: string | undefined, nowMs: number) {
  if (!nextAvailableAtUtc) {
    return 'soon'
  }

  const nextMs = Date.parse(nextAvailableAtUtc)
  if (Number.isNaN(nextMs) || nextMs <= nowMs) {
    return 'now'
  }

  const totalMinutes = Math.ceil((nextMs - nowMs) / 60000)
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60

  if (hours <= 0) {
    return `in ${minutes}m`
  }

  return `in ${hours}h ${minutes}m`
}
