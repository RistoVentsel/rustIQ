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
import { useAdsGate, useNow } from '../hooks'
import type { Language } from '../i18n/language'
import { useLanguage } from '../i18n/useLanguage'

type GameCardId = 'guessServer' | 'higherLower' | 'guessItem' | 'recipeGuess'
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

const gameCardBase = [
  {
    id: 'guessServer',
    image: '/rust-minigame-guess-server.png',
    emote: '/rust-minigame-emote-mystery.png',
    path: '/minigames/guess-the-server',
  },
  {
    id: 'higherLower',
    image: '/rust-minigame-higher-lower.png',
    emote: '/rust-minigame-emote-streak.png',
    path: '/minigames/higher-lower',
  },
  {
    id: 'guessItem',
    image: '/rust-minigame-guess-item.png',
    emote: '/rust-minigame-emote-hint.png',
    path: '/minigames/guess-the-item',
  },
  {
    id: 'recipeGuess',
    image: '/rust-minigame-recipe-guess.png',
    emote: '/rust-minigame-emote-correct.png',
    path: '/minigames/recipe-guess',
  },
] satisfies Array<{ id: GameCardId, image: string, emote: string, path: string }>

const difficultyOptions: Array<{ value: GuessDifficulty, poolSize: number }> = [
  { value: 'easy', poolSize: 50 },
  { value: 'medium', poolSize: 100 },
  { value: 'hard', poolSize: 250 },
]

const progressiveHintCount = 4
const cooldownMs = 24 * 60 * 60 * 1000
const higherLowerWinStreak = 10
const higherLowerRevealDelayMs = 1600
const higherLowerMetricOptions: Array<{ value: HigherLowerMetric }> = [
  { value: 'averagePlayers' },
  { value: 'currentPlayers' },
  { value: 'rank' },
]

const minigameCopy: Record<Language, {
  cards: Record<GameCardId, { title: string, status: string, description: string, meta: string }>
  difficulties: Record<GuessDifficulty, string>
  higherLowerMetrics: Record<HigherLowerMetric, { label: string, detail: string }>
  pageEyebrow: string
  pageHeading: string
  pageIntro: string
  todayLabel: string
  playableNow: string
  todayTitle: string
  todayDescription: string
  playGuessServer: string
  playHigherLower: string
  optionsLabel: string
  backToMinigames: string
  minigameEyebrow: string
  available: string
  soon: string
  now: string
  inMinutes: (minutes: number) => string
  inHoursMinutes: (hours: number, minutes: number) => string
  rankedServersError: string
  guess: {
    title: string
    description: string
    difficultyLabel: string
    top: (poolSize: number) => string
    loadingTitle: string
    loadingDescription: string
    loadErrorTitle: string
    loadErrorDescription: string
    warmupTitle: string
    warmupDescription: (required: number, current: number) => string
    dailyPuzzle: (poolSize: number) => string
    possibleServers: (poolSize: number) => string
    puzzleStatus: string
    attempts: string
    hints: string
    signal: string
    liveRankFeed: string
    startingHintLabel: string
    startingHintAria: string
    revealedHintsLabel: string
    hintNumber: (index: number) => string
    solved: string
    answerRevealed: string
    cooldownActive: string
    revealedAnswer: string
    answer: string
    yourGuess: string
    placeholder: string
    submit: string
    revealServer: string
    chooseServer: string
    alreadyTried: string
    historyLabel: string
    emptyHistory: string
  }
  higherLower: {
    title: string
    description: string
    metricLabel: string
    loadingTitle: string
    loadingDescription: string
    loadErrorTitle: string
    loadErrorDescription: string
    warmupTitle: string
    warmupDescription: (metric: string, current: number) => string
    round: (round: number) => string
    compareInstruction: string
    statusLabel: string
    streak: string
    best: string
    anchor: string
    challenger: string
    correct: string
    incorrect: string
    streakCleared: string
    runEnded: string
    cleared: (target: number) => string
    bestStreak: (best: number) => string
    actionsLabel: string
    higher: string
    higherDetail: string
    lower: string
    lowerDetail: string
    historyLabel: string
    emptyHistory: string
    previousAnchor: string
    previousChallenger: string
    historyLine: (choice: HigherLowerChoice, leftName: string, metricLabel: string) => string
    revealDescription: (rightName: string, rightValue: string, leftName: string, leftValue: string) => string
    maxPlayers: (maxPlayers: number) => string
  }
  labels: {
    unknown: string
    correct: string
    answerRanksHigher: string
    answerRanksLower: string
    averagePopulationAround: (floor: number, ceiling: number) => string
    averagePopulationWarming: string
    region: string
    serverType: string
    capacity: string
    wipeTiming: string
    rankRange: string
    serverRegion: (region: string) => string
    serverTypeIs: (serverType: string) => string
    maxPlayerCount: (maxPlayers: number) => string
    rankedBetween: (floor: number, ceiling: number) => string
    wipeUnknown: string
    wipedToday: string
    wipedOneDayAgo: string
    wipedDaysAgo: (days: number) => string
    type: string
    players: string
    max: string
    wipe: string
    rank: string
    avgPop: string
    sameDay: string
    newer: (days: number) => string
    older: (days: number) => string
    valueCorrect: (label: string) => string
    valueHigher: (label: string) => string
    valueLower: (label: string) => string
  }
}> = {
  en: {
    cards: {
      guessServer: {
        title: 'Guess the Server',
        status: 'Playable',
        description: 'Identify a ranked Rust server from comparison clues.',
        meta: 'Top 250 pool',
      },
      higherLower: {
        title: 'Higher / Lower',
        status: 'Playable',
        description: 'Pick which server has the higher population, peak, or rank.',
        meta: 'Population reads',
      },
      guessItem: {
        title: 'Guess the Item',
        status: 'Coming soon',
        description: 'Use workbench, category, cost, and recycle clues.',
        meta: 'Item logic',
      },
      recipeGuess: {
        title: 'Recipe Guess',
        status: 'Coming soon',
        description: 'See ingredients and name the item they craft.',
        meta: 'Craft clues',
      },
    },
    difficulties: {
      easy: 'Easy',
      medium: 'Medium',
      hard: 'Hard',
    },
    higherLowerMetrics: {
      averagePlayers: { label: 'Average pop', detail: 'Ranked average players' },
      currentPlayers: { label: 'Live players', detail: 'Current online count' },
      rank: { label: 'Rank', detail: 'Lower number wins' },
    },
    pageEyebrow: 'Minigames',
    pageHeading: 'Daily RustIQ challenges',
    pageIntro: 'Server knowledge, wipe timing, and Rust trivia in a daily format.',
    todayLabel: "Today's minigame lineup",
    playableNow: 'Playable now',
    todayTitle: 'Start with the daily server puzzle',
    todayDescription: 'Work from a live ranked server pool, use comparison clues, and keep guessing until the server clicks.',
    playGuessServer: 'Play Guess the Server',
    playHigherLower: 'Play Higher / Lower',
    optionsLabel: 'Minigame options',
    backToMinigames: 'Back to minigames',
    minigameEyebrow: 'Minigame',
    available: 'Available',
    soon: 'soon',
    now: 'now',
    inMinutes: (minutes) => `in ${minutes}m`,
    inHoursMinutes: (hours, minutes) => `in ${hours}h ${minutes}m`,
    rankedServersError: 'Unexpected error while loading ranked servers.',
    guess: {
      title: 'Guess the Server',
      description: 'The answer is selected from the RustIQ Top 50, Top 100, or Top 250. Each difficulty has its own 24 hour cooldown.',
      difficultyLabel: 'Guess the Server difficulty',
      top: (poolSize) => `Top ${poolSize}`,
      loadingTitle: 'Loading ranked servers',
      loadingDescription: 'Reading the stored Top 250 ranking from the server list API.',
      loadErrorTitle: 'Could not load ranked servers',
      loadErrorDescription: 'The game needs the RustIQ ranking pool before it can start.',
      warmupTitle: 'Ranking pool is still warming up',
      warmupDescription: (required, current) => `This difficulty needs ${required} ranked servers. Current ranked pool: ${current}.`,
      dailyPuzzle: (poolSize) => `Daily Top ${poolSize} puzzle`,
      possibleServers: (poolSize) => `${poolSize} possible servers. Guesses do not run out.`,
      puzzleStatus: 'Puzzle status',
      attempts: 'Attempts',
      hints: 'Hints',
      signal: 'Signal',
      liveRankFeed: 'Live rank feed',
      startingHintLabel: 'Starting hint',
      startingHintAria: 'Starting hint',
      revealedHintsLabel: 'Revealed hints',
      hintNumber: (index) => `Hint ${index}`,
      solved: 'Solved',
      answerRevealed: 'Answer revealed',
      cooldownActive: 'Cooldown active',
      revealedAnswer: 'Revealed answer',
      answer: 'Answer',
      yourGuess: 'Your guess',
      placeholder: 'Start typing a ranked server name',
      submit: 'Guess',
      revealServer: 'Reveal server',
      chooseServer: 'Choose a server from the dropdown.',
      alreadyTried: 'You already tried that server.',
      historyLabel: 'Guess history',
      emptyHistory: 'Each wrong guess reveals one more hint. After all hints are revealed, you can keep guessing or reveal the server.',
    },
    higherLower: {
      title: 'Higher / Lower',
      description: 'Pick whether the challenger is higher or lower than the anchor server. Reach a streak of 10 before one wrong answer.',
      metricLabel: 'Higher Lower metric',
      loadingTitle: 'Loading comparison pool',
      loadingDescription: 'Higher / Lower uses the same ranked server data as the daily server puzzle.',
      loadErrorTitle: 'Could not load comparison pool',
      loadErrorDescription: 'The game needs ranked servers before it can build matchups.',
      warmupTitle: 'Comparison pool is still warming up',
      warmupDescription: (metric, current) => `This mode needs at least 12 servers with ${metric.toLowerCase()} data. Current pool: ${current}.`,
      round: (round) => `Round ${round}`,
      compareInstruction: 'Compare the hidden challenger against the revealed anchor server.',
      statusLabel: 'Higher Lower status',
      streak: 'Streak',
      best: 'Best',
      anchor: 'Anchor',
      challenger: 'Challenger',
      correct: 'Correct',
      incorrect: 'Incorrect',
      streakCleared: 'Streak cleared',
      runEnded: 'Run ended',
      cleared: (target) => `You cleared ${target} in a row.`,
      bestStreak: (best) => `Best streak: ${best}.`,
      actionsLabel: 'Choose challenger comparison',
      higher: 'Higher',
      higherDetail: 'Challenger is above the anchor',
      lower: 'Lower',
      lowerDetail: 'Challenger is below the anchor',
      historyLabel: 'Higher Lower history',
      emptyHistory: 'Your last comparisons will appear here after each choice.',
      previousAnchor: 'Previous anchor',
      previousChallenger: 'Previous challenger',
      historyLine: (choice, leftName, metricLabel) => `${choice === 'higher' ? 'Higher' : 'Lower'} than ${leftName} on ${metricLabel.toLowerCase()}.`,
      revealDescription: (rightName, rightValue, leftName, leftValue) => `${rightName} was ${rightValue}. ${leftName} was ${leftValue}.`,
      maxPlayers: (maxPlayers) => `${maxPlayers} max`,
    },
    labels: {
      unknown: 'Unknown',
      correct: 'Correct',
      answerRanksHigher: 'Answer ranks higher',
      answerRanksLower: 'Answer ranks lower',
      averagePopulationAround: (floor, ceiling) => `Average population is around ${floor}-${ceiling}.`,
      averagePopulationWarming: 'Average population data is still warming up for this server.',
      region: 'Region',
      serverType: 'Server type',
      capacity: 'Capacity',
      wipeTiming: 'Wipe timing',
      rankRange: 'Rank range',
      serverRegion: (region) => `The server region is ${region}.`,
      serverTypeIs: (serverType) => `The server type is ${serverType}.`,
      maxPlayerCount: (maxPlayers) => `The max player count is ${maxPlayers}.`,
      rankedBetween: (floor, ceiling) => `The answer is ranked between #${floor} and #${ceiling}.`,
      wipeUnknown: 'Wipe timing is unknown for this server.',
      wipedToday: 'The server wiped today.',
      wipedOneDayAgo: 'The server wiped about 1 day ago.',
      wipedDaysAgo: (days) => `The server wiped about ${days} days ago.`,
      type: 'Type',
      players: 'Players',
      max: 'Max',
      wipe: 'Wipe',
      rank: 'Rank',
      avgPop: 'Avg pop',
      sameDay: 'Same day',
      newer: (days) => `Newer (${days}d)`,
      older: (days) => `Older (${days}d)`,
      valueCorrect: (label) => `${label}: correct`,
      valueHigher: (label) => `${label}: higher`,
      valueLower: (label) => `${label}: lower`,
    },
  },
  ru: {
    cards: {
      guessServer: {
        title: 'Угадай сервер',
        status: 'Доступно',
        description: 'Определите рейтинговый сервер Rust по подсказкам сравнения.',
        meta: 'Пул Top 250',
      },
      higherLower: {
        title: 'Больше / меньше',
        status: 'Доступно',
        description: 'Выберите, у какого сервера выше онлайн, пик или ранг.',
        meta: 'Данные онлайна',
      },
      guessItem: {
        title: 'Угадай предмет',
        status: 'Скоро',
        description: 'Используйте подсказки по верстаку, категории, стоимости и переработке.',
        meta: 'Логика предметов',
      },
      recipeGuess: {
        title: 'Угадай рецепт',
        status: 'Скоро',
        description: 'Посмотрите ингредиенты и назовите предмет, который они создают.',
        meta: 'Подсказки крафта',
      },
    },
    difficulties: {
      easy: 'Легко',
      medium: 'Средне',
      hard: 'Сложно',
    },
    higherLowerMetrics: {
      averagePlayers: { label: 'Средний онлайн', detail: 'Рейтинговое среднее игроков' },
      currentPlayers: { label: 'Live-игроки', detail: 'Текущий онлайн' },
      rank: { label: 'Ранг', detail: 'Меньшее число выигрывает' },
    },
    pageEyebrow: 'Мини-игры',
    pageHeading: 'Ежедневные задания RustIQ',
    pageIntro: 'Знание серверов, вайпы и Rust-тривия в ежедневном формате.',
    todayLabel: 'Линейка мини-игр на сегодня',
    playableNow: 'Доступно сейчас',
    todayTitle: 'Начните с ежедневной серверной загадки',
    todayDescription: 'Используйте актуальный пул рейтинговых серверов, подсказки сравнения и угадывайте, пока сервер не станет очевидным.',
    playGuessServer: 'Играть в Угадай сервер',
    playHigherLower: 'Играть в Больше / меньше',
    optionsLabel: 'Варианты мини-игр',
    backToMinigames: 'Назад к мини-играм',
    minigameEyebrow: 'Мини-игра',
    available: 'Доступно',
    soon: 'скоро',
    now: 'сейчас',
    inMinutes: (minutes) => `через ${minutes} мин`,
    inHoursMinutes: (hours, minutes) => `через ${hours} ч ${minutes} мин`,
    rankedServersError: 'Неожиданная ошибка при загрузке рейтинговых серверов.',
    guess: {
      title: 'Угадай сервер',
      description: 'Ответ выбирается из RustIQ Top 50, Top 100 или Top 250. У каждой сложности свой перерыв на 24 часа.',
      difficultyLabel: 'Сложность Угадай сервер',
      top: (poolSize) => `Top ${poolSize}`,
      loadingTitle: 'Загрузка рейтинговых серверов',
      loadingDescription: 'Читаем сохраненный рейтинг Top 250 из API списка серверов.',
      loadErrorTitle: 'Не удалось загрузить рейтинговые серверы',
      loadErrorDescription: 'Игре нужен рейтинговый пул RustIQ перед стартом.',
      warmupTitle: 'Рейтинговый пул еще прогревается',
      warmupDescription: (required, current) => `Для этой сложности нужно ${required} рейтинговых серверов. Сейчас в пуле: ${current}.`,
      dailyPuzzle: (poolSize) => `Ежедневная загадка Top ${poolSize}`,
      possibleServers: (poolSize) => `${poolSize} возможных серверов. Попытки не заканчиваются.`,
      puzzleStatus: 'Статус загадки',
      attempts: 'Попытки',
      hints: 'Подсказки',
      signal: 'Сигнал',
      liveRankFeed: 'Live-рейтинг',
      startingHintLabel: 'Стартовая подсказка',
      startingHintAria: 'Стартовая подсказка',
      revealedHintsLabel: 'Открытые подсказки',
      hintNumber: (index) => `Подсказка ${index}`,
      solved: 'Решено',
      answerRevealed: 'Ответ открыт',
      cooldownActive: 'Перерыв активен',
      revealedAnswer: 'Открытый ответ',
      answer: 'Ответ',
      yourGuess: 'Ваш вариант',
      placeholder: 'Начните вводить название рейтингового сервера',
      submit: 'Угадать',
      revealServer: 'Показать сервер',
      chooseServer: 'Выберите сервер из выпадающего списка.',
      alreadyTried: 'Вы уже пробовали этот сервер.',
      historyLabel: 'История попыток',
      emptyHistory: 'Каждая неверная попытка открывает еще одну подсказку. После всех подсказок можно продолжать угадывать или открыть сервер.',
    },
    higherLower: {
      title: 'Больше / меньше',
      description: 'Выберите, выше или ниже претендент относительно опорного сервера. Наберите серию из 10 до одной ошибки.',
      metricLabel: 'Метрика Больше / меньше',
      loadingTitle: 'Загрузка пула сравнения',
      loadingDescription: 'Больше / меньше использует те же рейтинговые данные серверов, что и ежедневная серверная загадка.',
      loadErrorTitle: 'Не удалось загрузить пул сравнения',
      loadErrorDescription: 'Игре нужны рейтинговые серверы, чтобы строить пары.',
      warmupTitle: 'Пул сравнения еще прогревается',
      warmupDescription: (metric, current) => `Для режима нужно минимум 12 серверов с данными "${metric.toLowerCase()}". Сейчас в пуле: ${current}.`,
      round: (round) => `Раунд ${round}`,
      compareInstruction: 'Сравните скрытого претендента с открытым опорным сервером.',
      statusLabel: 'Статус Больше / меньше',
      streak: 'Серия',
      best: 'Лучший',
      anchor: 'Опорный',
      challenger: 'Претендент',
      correct: 'Верно',
      incorrect: 'Неверно',
      streakCleared: 'Серия пройдена',
      runEnded: 'Попытка завершена',
      cleared: (target) => `Вы прошли ${target} подряд.`,
      bestStreak: (best) => `Лучшая серия: ${best}.`,
      actionsLabel: 'Выберите сравнение претендента',
      higher: 'Больше',
      higherDetail: 'Претендент выше опорного',
      lower: 'Меньше',
      lowerDetail: 'Претендент ниже опорного',
      historyLabel: 'История Больше / меньше',
      emptyHistory: 'Последние сравнения появятся здесь после каждого выбора.',
      previousAnchor: 'Предыдущий опорный сервер',
      previousChallenger: 'Предыдущий претендент',
      historyLine: (choice, leftName, metricLabel) => `${choice === 'higher' ? 'Больше' : 'Меньше'}, чем ${leftName}, по метрике ${metricLabel.toLowerCase()}.`,
      revealDescription: (rightName, rightValue, leftName, leftValue) => `${rightName}: ${rightValue}. ${leftName}: ${leftValue}.`,
      maxPlayers: (maxPlayers) => `${maxPlayers} макс.`,
    },
    labels: {
      unknown: 'Неизвестно',
      correct: 'Верно',
      answerRanksHigher: 'Ответ выше в рейтинге',
      answerRanksLower: 'Ответ ниже в рейтинге',
      averagePopulationAround: (floor, ceiling) => `Средний онлайн примерно ${floor}-${ceiling}.`,
      averagePopulationWarming: 'Данные среднего онлайна для этого сервера еще прогреваются.',
      region: 'Регион',
      serverType: 'Тип сервера',
      capacity: 'Вместимость',
      wipeTiming: 'Вайп',
      rankRange: 'Диапазон ранга',
      serverRegion: (region) => `Регион сервера: ${region}.`,
      serverTypeIs: (serverType) => `Тип сервера: ${serverType}.`,
      maxPlayerCount: (maxPlayers) => `Максимум игроков: ${maxPlayers}.`,
      rankedBetween: (floor, ceiling) => `Ответ находится между #${floor} и #${ceiling}.`,
      wipeUnknown: 'Время вайпа для этого сервера неизвестно.',
      wipedToday: 'Сервер вайпнулся сегодня.',
      wipedOneDayAgo: 'Сервер вайпнулся примерно 1 день назад.',
      wipedDaysAgo: (days) => `Сервер вайпнулся примерно ${days} дн. назад.`,
      type: 'Тип',
      players: 'Игроки',
      max: 'Макс.',
      wipe: 'Вайп',
      rank: 'Ранг',
      avgPop: 'Ср. онлайн',
      sameDay: 'Тот же день',
      newer: (days) => `Новее (${days} дн.)`,
      older: (days) => `Старше (${days} дн.)`,
      valueCorrect: (label) => `${label}: верно`,
      valueHigher: (label) => `${label}: выше`,
      valueLower: (label) => `${label}: ниже`,
    },
  },
}

function getGameCards(language: Language) {
  return gameCardBase.map((game) => ({
    ...game,
    ...minigameCopy[language].cards[game.id],
  }))
}

function getDifficultyLabel(difficulty: GuessDifficulty, language: Language) {
  return minigameCopy[language].difficulties[difficulty]
}

function getHigherLowerMetricDefinition(metric: HigherLowerMetric, language: Language) {
  return {
    value: metric,
    ...minigameCopy[language].higherLowerMetrics[metric],
  }
}

export function MinigamesPage() {
  useAdsGate(true)

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
  return <ComingSoonMinigamePage gameId="guessItem" />
}

export function RecipeGuessMinigamePage() {
  return <ComingSoonMinigamePage gameId="recipeGuess" />
}

function MinigamesPageHeader() {
  const { language } = useLanguage()
  const copy = minigameCopy[language]

  return (
    <div className="page-copy">
      <p className="page-eyebrow">{copy.pageEyebrow}</p>
      <h2 className="page-heading">{copy.pageHeading}</h2>
      <p className="page-text">
        {copy.pageIntro}
      </p>
    </div>
  )
}

function MinigameTodayPanel() {
  const { language } = useLanguage()
  const copy = minigameCopy[language]

  return (
    <section className="minigame-today" aria-label={copy.todayLabel}>
      <div className="minigame-today__hero">
        <img src="/rust-minigame-guess-server.png" alt="" />
        <div className="minigame-today__overlay">
          <span className="minigame-option__status">{copy.playableNow}</span>
          <h3>{copy.todayTitle}</h3>
          <p>
            {copy.todayDescription}
          </p>
          <div className="minigame-today__actions">
            <Link to="/minigames/guess-the-server" className="button-link">
              {copy.playGuessServer}
            </Link>
            <Link to="/minigames/higher-lower" className="button-link button-link--secondary">
              {copy.playHigherLower}
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}

function MinigameCardGrid() {
  const { language } = useLanguage()
  const copy = minigameCopy[language]
  const gameCards = getGameCards(language)

  return (
    <section className="minigame-options" aria-label={copy.optionsLabel}>
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
        const isPlayable = game.id === 'guessServer' || game.id === 'higherLower'
        const className = isPlayable
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
  gameId,
}: {
  gameId: GameCardId
}) {
  const { language } = useLanguage()
  const copy = minigameCopy[language]
  const game = getGameCards(language).find((item) => item.id === gameId)
  useAdsGate(false)

  if (!game) {
    return null
  }

  return (
    <div className="page-stack minigames-page">
      <Link to="/minigames" className="knowledge-back-link">{copy.backToMinigames}</Link>
      <section className="minigame-coming-soon">
        <div className="minigame-coming-soon__media" aria-hidden="true">
          <img src={game.image} alt="" />
          <span>
            <img src={game.emote} alt="" />
          </span>
        </div>
        <div className="minigame-coming-soon__copy">
          <p className="page-eyebrow">{copy.minigameEyebrow}</p>
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
  const { language } = useLanguage()
  const copy = minigameCopy[language]
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
  const selectedDifficulty = difficultyOptions.find((item) => item.value === difficulty) ?? difficultyOptions[0]
  const difficultyStatuses = useMemo(
    () => Object.fromEntries(difficultyOptions.map((item) => [
      item.value,
      getDifficultyStatus(item.value, dailyDateKey, nowMs, language),
    ])) as Record<GuessDifficulty, DifficultyStatus>,
    [dailyDateKey, language, nowMs],
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
    () => answer ? getAnswerHints(answer, selectedDifficulty.poolSize, getPuzzleKey(storedState, difficulty), language) : [],
    [answer, selectedDifficulty.poolSize, difficulty, language, storedState],
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
    () => Object.fromEntries(higherLowerMetricOptions.map((item) => [
      item.value,
      getHigherLowerMetricStatus(item.value, dailyDateKey, nowMs, language),
    ])) as Record<HigherLowerMetric, DifficultyStatus>,
    [dailyDateKey, language, nowMs],
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
  const selectedHigherLowerMetric = getHigherLowerMetricDefinition(higherLowerMetric, language)
  const higherLowerRevealTone = higherLowerReveal
    ? higherLowerReveal.result.correct ? 'correct' : 'wrong'
    : null
  const higherLowerRevealDescription = higherLowerReveal
    ? getHigherLowerRevealDescription(higherLowerReveal.result, higherLowerReveal.left, higherLowerReveal.right, language)
    : ''
  const answerDisplayName = answer ? getServerDisplayName(answer) : copy.labels.unknown
  const rankedServersErrorDetail =
    rankedServersQuery.error instanceof ApiError
      ? `${rankedServersQuery.error.message} (${rankedServersQuery.error.url})`
      : copy.rankedServersError

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

  const hasPlayableContent = activeGame === 'guess'
    ? Boolean(answer) && candidatePool.length >= selectedDifficulty.poolSize
    : Boolean(higherLowerDisplayPair) && higherLowerPool.length >= 12
  useAdsGate(hasPlayableContent)

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
      setGuessError(copy.guess.chooseServer)
      return
    }

    if (storedState.attempts.includes(guessedServer.id)) {
      setGuessError(copy.guess.alreadyTried)
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
      <Link to="/minigames" className="knowledge-back-link">{copy.backToMinigames}</Link>

      {activeGame === 'guess' ? (
        <SectionCard
          title={copy.guess.title}
          description={copy.guess.description}
        >
        <div className="difficulty-selector" aria-label={copy.guess.difficultyLabel}>
          {difficultyOptions.map((item) => (
            (() => {
              const status = difficultyStatuses[item.value]

              return (
                <button
                  key={item.value}
                  type="button"
                  className={item.value === difficulty ? 'difficulty-button difficulty-button--active' : 'difficulty-button'}
                  onClick={() => setDifficulty(item.value)}
                >
                  <span>{getDifficultyLabel(item.value, language)}</span>
                  <small>{copy.guess.top(item.poolSize)}</small>
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
            title={copy.guess.loadingTitle}
            description={copy.guess.loadingDescription}
          />
        ) : null}

        {rankedServersQuery.isError ? (
          <StateNotice
            title={copy.guess.loadErrorTitle}
            description={copy.guess.loadErrorDescription}
            detail={rankedServersErrorDetail}
            tone="error"
          />
        ) : null}

        {!rankedServersQuery.isLoading && !rankedServersQuery.isError && candidatePool.length < selectedDifficulty.poolSize ? (
          <StateNotice
            title={copy.guess.warmupTitle}
            description={copy.guess.warmupDescription(selectedDifficulty.poolSize, candidatePool.length)}
            tone="warning"
          />
        ) : null}

        {answer && candidatePool.length >= selectedDifficulty.poolSize ? (
          <div className="guess-server-game">
            <div className="guess-server-game__status">
              <div>
                <span className="minigame-option__status">{getDifficultyLabel(selectedDifficulty.value, language)}</span>
                <h3>{copy.guess.dailyPuzzle(selectedDifficulty.poolSize)}</h3>
                <p className="page-text">
                  {copy.guess.possibleServers(selectedDifficulty.poolSize)}
                </p>
              </div>
              <div className="guess-server-game__stats" aria-label={copy.guess.puzzleStatus}>
                <div>
                  <span>{copy.guess.attempts}</span>
                  <strong>{storedState.attempts.length}</strong>
                </div>
                <div>
                  <span>{copy.guess.hints}</span>
                  <strong>{1 + (storedState.revealedHintCount ?? 0)}/{1 + progressiveHintCount}</strong>
                </div>
              </div>
              <div className="guess-server-game__visual" aria-hidden="true">
                <img src="/rust-minigame-guess-server.png" alt="" />
                <div>
                  <span>{copy.guess.signal}</span>
                  <strong>{copy.guess.liveRankFeed}</strong>
                </div>
              </div>
            </div>

            <div className="guess-server-hint" aria-label={copy.guess.startingHintAria}>
              <img className="guess-server-hint__emote" src="/rust-minigame-emote-hint.png" alt="" />
              <div>
                <span className="guess-server-hint__eyebrow">{copy.guess.startingHintLabel}</span>
                <h4>{getStartingHint(answer, language)}</h4>
              </div>
            </div>

            {revealedHints.length > 0 ? (
              <div className="guess-server-revealed-hints" aria-label={copy.guess.revealedHintsLabel}>
                {revealedHints.map((hint, index) => (
                  <div key={hint.label} className="guess-server-revealed-hint">
                    <span>{copy.guess.hintNumber(index + 2)}</span>
                    <strong>{hint.label}</strong>
                    <p>{hint.value}</p>
                  </div>
                ))}
              </div>
            ) : null}

            {isGameClosed ? (
              <StateNotice
                title={storedState.solved ? copy.guess.solved : storedState.revealedAnswer ? copy.guess.answerRevealed : copy.guess.cooldownActive}
                description={
                  <span className="guess-server-answer-notice">
                    <span>{storedState.revealedAnswer ? copy.guess.revealedAnswer : copy.guess.answer}</span>
                    <strong>{answerDisplayName}</strong>
                  </span>
                }
                tone={storedState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="guess-server-actions">
                <form className="guess-server-form" onSubmit={submitGuess}>
                  <label className="guess-server-input">
                    <span>{copy.guess.yourGuess}</span>
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
                      placeholder={copy.guess.placeholder}
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
                            <small>{formatCountry(server.country, language)} | {getRustServerTypeLabel(server.serverType, language)}</small>
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </label>

                  <button type="submit" className="button-link">
                    {copy.guess.submit}
                  </button>
                </form>

                {hasRevealedAllHints ? (
                  <button
                    type="button"
                    className="button-link button-link--secondary"
                    onClick={revealAnswer}
                  >
                    {copy.guess.revealServer}
                  </button>
                ) : null}
              </div>
            )}

            {guessError ? (
              <p className="guess-server-error">{guessError}</p>
            ) : null}

            <div className="guess-server-clues" aria-label={copy.guess.historyLabel}>
              {attemptedServers.length === 0 ? (
                <p className="page-text">{copy.guess.emptyHistory}</p>
              ) : (
                attemptedServers.map((server, index) => (
                  <GuessResultRow
                    key={server.id}
                    guess={server}
                    answer={answer}
                    attemptNumber={index + 1}
                    nowMs={nowMs}
                    clueOrder={clueOrder}
                    language={language}
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
          title={copy.higherLower.title}
          description={copy.higherLower.description}
        >
        <div className="higher-lower-modes" aria-label={copy.higherLower.metricLabel}>
          {higherLowerMetricOptions.map((item) => {
            const status = higherLowerMetricStatuses[item.value]
            const metricDefinition = getHigherLowerMetricDefinition(item.value, language)

            return (
              <button
                key={item.value}
                type="button"
                className={item.value === higherLowerMetric ? 'higher-lower-mode higher-lower-mode--active' : 'higher-lower-mode'}
                disabled={higherLowerIsRevealing}
                onClick={() => setHigherLowerMetric(item.value)}
              >
                <span>{metricDefinition.label}</span>
                <small>{metricDefinition.detail}</small>
                <strong className={status.isLocked ? 'difficulty-button__timer difficulty-button__timer--locked' : 'difficulty-button__timer'}>
                  {status.label}
                </strong>
              </button>
            )
          })}
        </div>

        {rankedServersQuery.isLoading ? (
          <StateNotice
            title={copy.higherLower.loadingTitle}
            description={copy.higherLower.loadingDescription}
          />
        ) : null}

        {rankedServersQuery.isError ? (
          <StateNotice
            title={copy.higherLower.loadErrorTitle}
            description={copy.higherLower.loadErrorDescription}
            detail={rankedServersErrorDetail}
            tone="error"
          />
        ) : null}

        {!rankedServersQuery.isLoading && !rankedServersQuery.isError && higherLowerPool.length < 12 ? (
          <StateNotice
            title={copy.higherLower.warmupTitle}
            description={copy.higherLower.warmupDescription(selectedHigherLowerMetric.label, higherLowerPool.length)}
            tone="warning"
          />
        ) : null}

        {higherLowerDisplayPair && higherLowerPool.length >= 12 ? (
          <div className={higherLowerRevealTone ? `higher-lower-game higher-lower-game--${higherLowerRevealTone}` : 'higher-lower-game'}>
            <div className="higher-lower-header">
              <div>
                <span className="minigame-option__status">{selectedHigherLowerMetric.label}</span>
                <h3>{copy.higherLower.round(higherLowerState.roundIndex + 1)}</h3>
                <p className="page-text">
                  {copy.higherLower.compareInstruction}
                </p>
              </div>
              <div className="guess-server-game__stats" aria-label={copy.higherLower.statusLabel}>
                <div>
                  <span>{copy.higherLower.streak}</span>
                  <strong>{higherLowerVisibleStreak}/{higherLowerWinStreak}</strong>
                </div>
                <div>
                  <span>{copy.higherLower.best}</span>
                  <strong>{higherLowerVisibleBestStreak}</strong>
                </div>
              </div>
            </div>

            <div className="higher-lower-board">
              <HigherLowerServerCard
                server={higherLowerDisplayPair.left}
                metric={higherLowerMetric}
                label={copy.higherLower.anchor}
                isValueHidden={false}
                language={language}
              />

              <div className="higher-lower-versus" aria-hidden="true">
                <img src="/rust-minigame-emote-streak.png" alt="" />
                <span>VS</span>
              </div>

              <HigherLowerServerCard
                server={higherLowerDisplayPair.right}
                metric={higherLowerMetric}
                label={copy.higherLower.challenger}
                isValueHidden={!higherLowerIsClosed && !higherLowerIsRevealing}
                resultTone={higherLowerRevealTone ?? undefined}
                language={language}
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
                  <strong>{higherLowerReveal.result.correct ? copy.higherLower.correct : copy.higherLower.incorrect}</strong>
                  <span>{higherLowerRevealDescription}</span>
                </div>
              </div>
            ) : higherLowerIsClosed ? (
              <StateNotice
                title={higherLowerState.solved ? copy.higherLower.streakCleared : copy.higherLower.runEnded}
                description={
                  higherLowerState.solved
                    ? copy.higherLower.cleared(higherLowerWinStreak)
                    : copy.higherLower.bestStreak(higherLowerState.bestStreak)
                }
                tone={higherLowerState.solved ? 'success' : 'warning'}
              />
            ) : (
              <div className="higher-lower-actions" aria-label={copy.higherLower.actionsLabel}>
                <button
                  type="button"
                  className="higher-lower-choice higher-lower-choice--higher"
                  onClick={() => submitHigherLowerChoice('higher')}
                >
                  <span className="higher-lower-choice__arrow" aria-hidden="true">↑</span>
                  <span>
                    <strong>{copy.higherLower.higher}</strong>
                    <small>{copy.higherLower.higherDetail}</small>
                  </span>
                </button>
                <button
                  type="button"
                  className="higher-lower-choice higher-lower-choice--lower"
                  onClick={() => submitHigherLowerChoice('lower')}
                >
                  <span className="higher-lower-choice__arrow" aria-hidden="true">↓</span>
                  <span>
                    <strong>{copy.higherLower.lower}</strong>
                    <small>{copy.higherLower.lowerDetail}</small>
                  </span>
                </button>
              </div>
            )}

            <div className="higher-lower-history" aria-label={copy.higherLower.historyLabel}>
              {higherLowerState.history.length === 0 ? (
                <p className="page-text">{copy.higherLower.emptyHistory}</p>
              ) : (
                higherLowerState.history.map((round, index) => (
                  <HigherLowerHistoryRow
                    key={`${round.leftId}:${round.rightId}:${index}`}
                    round={round}
                    servers={higherLowerPool}
                    language={language}
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
  language,
}: {
  server: ServerListItem
  metric: HigherLowerMetric
  label: string
  isValueHidden: boolean
  resultTone?: 'correct' | 'wrong'
  language: Language
}) {
  const copy = minigameCopy[language]
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
        <span>{getHigherLowerMetricLabel(metric, language)}</span>
        <strong>{isValueHidden ? '???' : formatHigherLowerMetricValue(metricValue, metric, language)}</strong>
      </div>
      <div className="higher-lower-server__meta">
        <span>{formatCountry(server.country, language)}</span>
        <span>{getRustServerTypeLabel(server.serverType, language)}</span>
        <span>{copy.higherLower.maxPlayers(server.maxPlayers)}</span>
      </div>
    </article>
  )
}

function HigherLowerHistoryRow({
  round,
  servers,
  language,
}: {
  round: HigherLowerRoundResult
  servers: ServerListItem[]
  language: Language
}) {
  const copy = minigameCopy[language]
  const left = servers.find((server) => server.id === round.leftId)
  const right = servers.find((server) => server.id === round.rightId)
  const leftName = left ? getServerDisplayName(left) : copy.higherLower.previousAnchor
  const rightName = right ? getServerDisplayName(right) : copy.higherLower.previousChallenger

  return (
    <article className={round.correct ? 'higher-lower-history-row higher-lower-history-row--correct' : 'higher-lower-history-row'}>
      <img
        src={round.correct ? '/rust-minigame-emote-correct.png' : '/rust-minigame-emote-wrong.png'}
        alt=""
      />
      <div>
        <strong>{rightName}</strong>
        <p>
          {copy.higherLower.historyLine(round.choice, leftName, getHigherLowerMetricLabel(round.metric, language))}
        </p>
      </div>
      <span>
        {formatHigherLowerMetricValue(round.leftValue, round.metric, language)} / {formatHigherLowerMetricValue(round.rightValue, round.metric, language)}
      </span>
    </article>
  )
}

function getHigherLowerRevealDescription(
  round: HigherLowerRoundResult,
  left: ServerListItem,
  right: ServerListItem,
  language: Language,
) {
  const copy = minigameCopy[language]

  return copy.higherLower.revealDescription(
    getServerDisplayName(right),
    formatHigherLowerMetricValue(round.rightValue, round.metric, language),
    getServerDisplayName(left),
    formatHigherLowerMetricValue(round.leftValue, round.metric, language),
  )
}

function GuessResultRow({
  guess,
  answer,
  attemptNumber,
  nowMs,
  clueOrder,
  language,
}: {
  guess: ServerListItem
  answer: ServerListItem
  attemptNumber: number
  nowMs: number
  clueOrder: ClueKind[]
  language: Language
}) {
  const isCorrect = guess.id === answer.id
  const clues = buildGuessClues(guess, answer, nowMs, language)

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
  language: Language,
): DifficultyStatus {
  const copy = minigameCopy[language]
  const state = normalizeStoredGuessState(readStoredGuessState(difficulty), dateKey, nowMs, difficulty)
  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  const isLocked = Boolean(state.completedAtUtc && nextAvailableMs && nextAvailableMs > nowMs)

  return {
    isLocked,
    label: isLocked ? formatCooldown(state.nextAvailableAtUtc, nowMs, language) : copy.available,
  }
}

function getHigherLowerMetricStatus(
  metric: HigherLowerMetric,
  dateKey: string,
  nowMs: number,
  language: Language,
): DifficultyStatus {
  const copy = minigameCopy[language]
  const state = normalizeStoredHigherLowerState(readStoredHigherLowerState(metric), dateKey, nowMs, metric)
  const nextAvailableMs = state.nextAvailableAtUtc ? Date.parse(state.nextAvailableAtUtc) : null
  const isLocked = Boolean(state.completedAtUtc && nextAvailableMs && nextAvailableMs > nowMs)

  return {
    isLocked,
    label: isLocked ? formatCooldown(state.nextAvailableAtUtc, nowMs, language) : copy.available,
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

function getHigherLowerMetricLabel(metric: HigherLowerMetric, language: Language = 'en') {
  return minigameCopy[language].higherLowerMetrics[metric].label
}

function formatHigherLowerMetricValue(value: number | null, metric: HigherLowerMetric, language: Language = 'en') {
  if (value === null) {
    return minigameCopy[language].labels.unknown
  }

  if (metric === 'rank') {
    return `#${Math.round(value)}`
  }

  return Math.round(value).toLocaleString(language === 'ru' ? 'ru-RU' : 'en')
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

function compareRank(guess: ServerListItem, answer: ServerListItem, language: Language) {
  const labels = minigameCopy[language].labels

  if (guess.popularityRank === answer.popularityRank) {
    return labels.correct
  }

  if (!guess.popularityRank || !answer.popularityRank) {
    return labels.unknown
  }

  return answer.popularityRank < guess.popularityRank ? labels.answerRanksHigher : labels.answerRanksLower
}

function getStartingHint(answer: ServerListItem, language: Language) {
  const labels = minigameCopy[language].labels
  const averagePlayers = answer.rankedAveragePlayers

  if (averagePlayers !== null && averagePlayers !== undefined) {
    const populationFloor = Math.floor(averagePlayers / 50) * 50
    const populationCeiling = populationFloor + 49

    return labels.averagePopulationAround(populationFloor, populationCeiling)
  }

  return labels.averagePopulationWarming
}

function getAnswerHints(
  answer: ServerListItem,
  poolSize: number,
  puzzleKey: string,
  language: Language,
): GuessClue[] {
  const labels = minigameCopy[language].labels
  const shuffledHints = shuffleWithSeed<GuessClue>([
    {
      label: labels.region,
      value: labels.serverRegion(formatCountry(answer.country, language)),
    },
    {
      label: labels.serverType,
      value: labels.serverTypeIs(getRustServerTypeLabel(answer.serverType, language)),
    },
    {
      label: labels.capacity,
      value: labels.maxPlayerCount(answer.maxPlayers),
    },
    {
      label: labels.wipeTiming,
      value: getWipeHint(answer.lastWipeAtUtc, language),
    },
  ], hashString(`${puzzleKey}:answer-hints`))

  return [
    ...shuffledHints.slice(0, progressiveHintCount - 1),
    {
      label: labels.rankRange,
      value: getRankRangeHint(answer, poolSize, language),
    },
  ]
}

function getRankRangeHint(answer: ServerListItem, poolSize: number, language: Language) {
  const labels = minigameCopy[language].labels
  const rank = answer.popularityRank ?? poolSize
  const rankBucketSize = poolSize <= 50 ? 10 : poolSize <= 100 ? 20 : 50
  const rankFloor = Math.floor((rank - 1) / rankBucketSize) * rankBucketSize + 1
  const rankCeiling = Math.min(rankFloor + rankBucketSize - 1, poolSize)

  return labels.rankedBetween(rankFloor, rankCeiling)
}

function getWipeHint(lastWipeAtUtc: string | null, language: Language) {
  const labels = minigameCopy[language].labels

  if (!lastWipeAtUtc) {
    return labels.wipeUnknown
  }

  const parsedWipe = Date.parse(lastWipeAtUtc)
  if (Number.isNaN(parsedWipe)) {
    return labels.wipeUnknown
  }

  const ageDays = Math.max(0, Math.round((Date.now() - parsedWipe) / (24 * 60 * 60 * 1000)))

  if (ageDays === 0) {
    return labels.wipedToday
  }

  if (ageDays === 1) {
    return labels.wipedOneDayAgo
  }

  return labels.wipedDaysAgo(ageDays)
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
  language: Language,
): Record<ClueKind, GuessClue> {
  const labels = minigameCopy[language].labels
  const rankComparison = compareRank(guess, answer, language)
  const playersComparison = compareNumber(guess.rankedAveragePlayers, answer.rankedAveragePlayers, labels.avgPop, language)
  const maxPlayersComparison = compareNumber(guess.maxPlayers, answer.maxPlayers, labels.max, language)
  const wipeComparison = compareWipe(guess.lastWipeAtUtc, answer.lastWipeAtUtc, nowMs, language)

  return {
    region: {
      label: labels.region,
      value: formatCountry(guess.country, language),
      tone: guess.country === answer.country ? 'correct' : 'wrong',
    },
    type: {
      label: labels.type,
      value: getRustServerTypeLabel(guess.serverType, language),
      tone: guess.serverType === answer.serverType ? 'correct' : 'wrong',
    },
    players: {
      label: labels.players,
      value: playersComparison,
      tone: getTone(playersComparison, labels.valueCorrect(labels.avgPop), labels.unknown),
    },
    maxPlayers: {
      label: labels.max,
      value: maxPlayersComparison,
      tone: getTone(maxPlayersComparison, labels.valueCorrect(labels.max), labels.unknown),
    },
    wipe: {
      label: labels.wipe,
      value: wipeComparison,
      tone: getTone(wipeComparison, labels.sameDay, labels.unknown),
    },
    rank: {
      label: labels.rank,
      value: rankComparison,
      tone: getTone(rankComparison, labels.correct, labels.unknown),
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
  language: Language,
) {
  const labels = minigameCopy[language].labels

  if (guessValue === null || guessValue === undefined || answerValue === null || answerValue === undefined) {
    return labels.unknown
  }

  if (Math.round(guessValue) === Math.round(answerValue)) {
    return labels.valueCorrect(label)
  }

  return answerValue > guessValue ? labels.valueHigher(label) : labels.valueLower(label)
}

function compareWipe(
  guessWipeAtUtc: string | null,
  answerWipeAtUtc: string | null,
  nowMs: number,
  language: Language,
) {
  const labels = minigameCopy[language].labels

  if (!guessWipeAtUtc || !answerWipeAtUtc) {
    return labels.unknown
  }

  const guessMs = Date.parse(guessWipeAtUtc)
  const answerMs = Date.parse(answerWipeAtUtc)

  if (Number.isNaN(guessMs) || Number.isNaN(answerMs)) {
    return labels.unknown
  }

  const dayMs = 24 * 60 * 60 * 1000
  if (Math.abs(guessMs - answerMs) < dayMs) {
    return labels.sameDay
  }

  const answerAgeDays = Math.round((nowMs - answerMs) / dayMs)
  return answerMs > guessMs ? labels.newer(answerAgeDays) : labels.older(answerAgeDays)
}

function getTone(value: string, correctValue: string, unknownValue: string): 'correct' | 'wrong' | 'neutral' {
  if (value === correctValue) {
    return 'correct'
  }

  if (value === unknownValue) {
    return 'neutral'
  }

  return 'wrong'
}

function formatCooldown(nextAvailableAtUtc: string | undefined, nowMs: number, language: Language) {
  const copy = minigameCopy[language]

  if (!nextAvailableAtUtc) {
    return copy.soon
  }

  const nextMs = Date.parse(nextAvailableAtUtc)
  if (Number.isNaN(nextMs) || nextMs <= nowMs) {
    return copy.now
  }

  const totalMinutes = Math.ceil((nextMs - nowMs) / 60000)
  const hours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60

  if (hours <= 0) {
    return copy.inMinutes(minutes)
  }

  return copy.inHoursMinutes(hours, minutes)
}
