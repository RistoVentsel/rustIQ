import { useQuery } from '@tanstack/react-query'
import { type FormEvent, useEffect, useMemo, useState } from 'react'

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

const gameCards = [
  {
    title: 'Guess the Server',
    status: 'Playable',
    description: 'Identify a ranked Rust server from comparison clues.',
  },
  {
    title: 'Higher / Lower',
    status: 'Coming soon',
    description: 'Pick which server has the higher population, peak, or rank.',
  },
  {
    title: 'Guess the Item',
    status: 'Coming soon',
    description: 'Use workbench, category, cost, and recycle clues.',
  },
  {
    title: 'Recipe Guess',
    status: 'Coming soon',
    description: 'See ingredients and name the item they craft.',
  },
] as const

const difficulties: Array<{ value: GuessDifficulty, label: string, poolSize: number }> = [
  { value: 'easy', label: 'Easy', poolSize: 50 },
  { value: 'medium', label: 'Medium', poolSize: 100 },
  { value: 'hard', label: 'Hard', poolSize: 250 },
]

const progressiveHintCount = 4
const cooldownMs = 24 * 60 * 60 * 1000

export function MinigamesPage() {
  const nowMs = useNow()
  const [difficulty, setDifficulty] = useState<GuessDifficulty>('easy')
  const [guessInput, setGuessInput] = useState('')
  const [selectedGuessId, setSelectedGuessId] = useState<string | null>(null)
  const [guessError, setGuessError] = useState<string | null>(null)
  const [isSuggestionsOpen, setIsSuggestionsOpen] = useState(false)
  const [storedState, setStoredState] = useState<StoredGuessState>(() =>
    normalizeStoredGuessState(readStoredGuessState('easy'), getDailyDateKey(), nowMs, 'easy'))

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
  const answerDisplayName = answer ? getServerDisplayName(answer) : 'Unknown'
  const rankedServersErrorDetail =
    rankedServersQuery.error instanceof ApiError
      ? `${rankedServersQuery.error.message} (${rankedServersQuery.error.url})`
      : 'Unexpected error while loading ranked servers.'

  useEffect(() => {
    const nextState = normalizeStoredGuessState(readStoredGuessState(difficulty), dailyDateKey, Date.now(), difficulty)
    setStoredState(nextState)
    setGuessInput('')
    setSelectedGuessId(null)
    setGuessError(null)
    setIsSuggestionsOpen(false)
  }, [difficulty, dailyDateKey])

  useEffect(() => {
    const nextAvailableMs = storedState.nextAvailableAtUtc ? Date.parse(storedState.nextAvailableAtUtc) : null
    if (storedState.completedAtUtc && nextAvailableMs && nextAvailableMs <= nowMs) {
      updateStoredState(createFreshGuessState(difficulty, dailyDateKey, storedState.answerId ?? storedState.previousAnswerId))
      setGuessInput('')
      setSelectedGuessId(null)
      setGuessError(null)
      setIsSuggestionsOpen(false)
    }
  }, [dailyDateKey, nowMs, storedState])

  function updateStoredState(nextState: StoredGuessState) {
    setStoredState(nextState)
    window.localStorage.setItem(getStorageKey(difficulty), JSON.stringify(nextState))
  }

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

  return (
    <div className="page-stack minigames-page">
      <div className="page-copy">
        <p className="page-eyebrow">Minigames</p>
        <h2 className="page-heading">Daily RustIQ challenges</h2>
        <p className="page-text">
          Server knowledge, wipe timing, and Rust trivia in a daily format.
        </p>
      </div>

      <section className="minigame-options" aria-label="Minigame options">
        {gameCards.map((game) => (
          <article
            key={game.title}
            className={game.status === 'Playable' ? 'minigame-option minigame-option--active' : 'minigame-option'}
          >
            <div>
              <span className="minigame-option__status">{game.status}</span>
              <h3>{game.title}</h3>
            </div>
            <p>{game.description}</p>
          </article>
        ))}
      </section>

      <SectionCard
        title="Guess the Server"
        description="The answer is selected from the RustIQ Top 50, Top 100, or Top 250. Each difficulty has its own 24 hour cooldown."
      >
        <div className="difficulty-selector" aria-label="Guess the Server difficulty">
          {difficulties.map((item) => (
            <button
              key={item.value}
              type="button"
              className={item.value === difficulty ? 'difficulty-button difficulty-button--active' : 'difficulty-button'}
              onClick={() => setDifficulty(item.value)}
            >
              <span>{item.label}</span>
              <small>Top {item.poolSize}</small>
            </button>
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
                <div>
                  <span>Status</span>
                  <strong>{isCooldownActive ? formatCooldown(storedState.nextAvailableAtUtc, nowMs) : 'Live'}</strong>
                </div>
              </div>
            </div>

            <div className="guess-server-hint" aria-label="Starting hint">
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
                title={storedState.solved ? 'Solved for this cooldown' : 'Cooldown active'}
                description={
                  storedState.solved
                    ? `Answer: ${answerDisplayName}. This difficulty unlocks again ${formatCooldown(storedState.nextAvailableAtUtc, nowMs)}.`
                    : storedState.revealedAnswer
                    ? `Revealed answer: ${answerDisplayName}. This difficulty unlocks again ${formatCooldown(storedState.nextAvailableAtUtc, nowMs)}.`
                    : `Answer: ${answerDisplayName}. This difficulty unlocks again ${formatCooldown(storedState.nextAvailableAtUtc, nowMs)}.`
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
    </div>
  )
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
    <article className={isCorrect ? 'guess-result guess-result--correct' : 'guess-result'}>
      <div className="guess-result__header">
        <span>#{attemptNumber}</span>
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

function getPuzzleKey(state: StoredGuessState, difficulty: GuessDifficulty) {
  return state.puzzleKey ?? `${state.dateKey}:${difficulty}:legacy`
}

function createPuzzleKey(difficulty: GuessDifficulty, timestampMs = Date.now()) {
  return `${difficulty}:${timestampMs}`
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
      value: compareNumber(guess.rankedAveragePlayers, answer.rankedAveragePlayers, 'Avg pop'),
    },
    maxPlayers: {
      label: 'Max',
      value: compareNumber(guess.maxPlayers, answer.maxPlayers, 'Max'),
    },
    wipe: {
      label: 'Wipe',
      value: compareWipe(guess.lastWipeAtUtc, answer.lastWipeAtUtc, nowMs),
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
