import type { Language } from '../../i18n/language'
import type { RustItemCategory } from './rustItemData'

export type ItemDifficulty = 'easy' | 'medium' | 'hard'

export const itemDifficultyOptions: Array<{
  value: ItemDifficulty
  maxWorkbenchLevel: 0 | 1 | 2 | 3
}> = [
  { value: 'easy', maxWorkbenchLevel: 0 },
  { value: 'medium', maxWorkbenchLevel: 1 },
  { value: 'hard', maxWorkbenchLevel: 3 },
]

export const itemHintCount = 4
export const recipeWinStreak = 10
export const recipeOptionCount = 4
export const recipeRevealDelayMs = 1700

type CommonCopy = {
  available: string
  soon: string
  now: string
  inMinutes: (minutes: number) => string
  inHoursMinutes: (hours: number, minutes: number) => string
  backToMinigames: string
  difficulties: Record<ItemDifficulty, string>
  categories: Record<RustItemCategory, string>
  workbench: Record<0 | 1 | 2 | 3, string>
  itemPool: (count: number) => string
}

export type ItemGameCopy = CommonCopy & {
  guessItem: {
    title: string
    description: string
    difficultyLabel: string
    howToPlayTitle: string
    howToPlay: string[]
    puzzleTitle: string
    poolNote: (count: number) => string
    attempts: string
    hints: string
    startingClueLabel: string
    startingClue: (category: string) => string
    revealedHintsLabel: string
    hintNumber: (index: number) => string
    hintWorkbenchLabel: string
    hintWorkbenchValue: (workbench: string) => string
    hintIngredientsLabel: string
    hintIngredientsValue: (distinct: number, total: number) => string
    hintMainIngredientLabel: string
    hintMainIngredientValue: (quantity: number, name: string) => string
    hintRecycleLabel: string
    hintRecycleValue: (output: string) => string
    hintRecycleUnknown: string
    yourGuess: string
    placeholder: string
    submit: string
    revealItem: string
    chooseItem: string
    alreadyTried: string
    historyLabel: string
    emptyHistory: string
    solved: string
    answerRevealed: string
    cooldownActive: string
    answer: string
    revealedAnswer: string
    sameCategory: string
    otherCategory: string
    sameWorkbench: string
    higherWorkbench: string
    lowerWorkbench: string
    sourceNote: string
  }
  recipeGuess: {
    title: string
    description: string
    howToPlayTitle: string
    howToPlay: string[]
    round: (round: number) => string
    prompt: string
    ingredientsLabel: string
    workbenchLabel: string
    streak: string
    best: string
    correct: string
    incorrect: string
    correctAnswerWas: (name: string) => string
    runEnded: string
    cleared: (target: number) => string
    bestStreak: (best: number) => string
    cooldownActive: string
    historyLabel: string
    emptyHistory: string
    historyLine: (guessed: string, answer: string) => string
    sourceNote: string
  }
}

const categoriesEn: Record<RustItemCategory, string> = {
  weapon: 'Weapon',
  tool: 'Tool',
  construction: 'Construction',
  deployable: 'Deployable',
  medical: 'Medical',
  explosive: 'Explosive',
}

const categoriesRu: Record<RustItemCategory, string> = {
  weapon: 'Оружие',
  tool: 'Инструмент',
  construction: 'Строительство',
  deployable: 'Постройка',
  medical: 'Медицина',
  explosive: 'Взрывчатка',
}

export const itemGameCopy: Record<Language, ItemGameCopy> = {
  en: {
    available: 'Available',
    soon: 'soon',
    now: 'now',
    inMinutes: (minutes) => `in ${minutes}m`,
    inHoursMinutes: (hours, minutes) => `in ${hours}h ${minutes}m`,
    backToMinigames: 'Back to minigames',
    difficulties: {
      easy: 'Easy',
      medium: 'Medium',
      hard: 'Hard',
    },
    categories: categoriesEn,
    workbench: {
      0: 'No workbench',
      1: 'Workbench Level 1',
      2: 'Workbench Level 2',
      3: 'Workbench Level 3',
    },
    itemPool: (count) => `${count} items`,
    guessItem: {
      title: 'Guess the Item',
      description: 'Identify a Rust item from its workbench tier, crafting cost, and recycle output.',
      difficultyLabel: 'Difficulty',
      howToPlayTitle: 'How to play',
      howToPlay: [
        'You start with the item category. Each wrong guess reveals one more clue: workbench level, then how many resources the recipe takes, then its largest ingredient, then what it returns in a recycler.',
        'Every guess is also compared against the answer, so a wrong item still tells you whether the answer sits at a higher or lower workbench tier.',
        'Easy uses items you can craft with no workbench. Medium adds Workbench Level 1. Hard uses every item in the set, up to Workbench Level 3.',
        'One puzzle per difficulty per day. Solving or revealing starts a 24 hour cooldown on that difficulty.',
      ],
      puzzleTitle: 'Daily item puzzle',
      poolNote: (count) => `The answer is one of ${count} craftable Rust items.`,
      attempts: 'Attempts',
      hints: 'Clues',
      startingClueLabel: 'Starting clue',
      startingClue: (category) => `This item is categorised as: ${category}`,
      revealedHintsLabel: 'Revealed clues',
      hintNumber: (index) => `Clue ${index}`,
      hintWorkbenchLabel: 'Workbench level',
      hintWorkbenchValue: (workbench) => workbench,
      hintIngredientsLabel: 'Recipe shape',
      hintIngredientsValue: (distinct, total) =>
        `${distinct} different ingredient${distinct === 1 ? '' : 's'}, ${total} resources in total.`,
      hintMainIngredientLabel: 'Largest ingredient',
      hintMainIngredientValue: (quantity, name) => `${quantity} x ${name}`,
      hintRecycleLabel: 'Recycler output',
      hintRecycleValue: (output) => output,
      hintRecycleUnknown: 'Recycler output is not recorded for this item.',
      yourGuess: 'Your guess',
      placeholder: 'Start typing an item name',
      submit: 'Guess',
      revealItem: 'Reveal the item',
      chooseItem: 'Pick an item from the list.',
      alreadyTried: 'You already tried that item.',
      historyLabel: 'Previous guesses',
      emptyHistory: 'No guesses yet.',
      solved: 'Solved',
      answerRevealed: 'Answer revealed',
      cooldownActive: 'Come back tomorrow',
      answer: 'The item was',
      revealedAnswer: 'The item was',
      sameCategory: 'Same category',
      otherCategory: 'Different category',
      sameWorkbench: 'Same workbench level',
      higherWorkbench: 'Answer needs a higher workbench',
      lowerWorkbench: 'Answer needs a lower workbench',
      sourceNote: 'Recipes and recycle outputs are taken from the official Facepunch wiki.',
    },
    recipeGuess: {
      title: 'Recipe Guess',
      description: 'See a Rust crafting recipe and name the item it makes.',
      howToPlayTitle: 'How to play',
      howToPlay: [
        'Each round shows one full crafting recipe and the workbench level it needs. Pick which of the four items that recipe builds.',
        'Decoys are drawn from the same workbench tier, so the recipe itself is the thing that separates them.',
        'A correct answer extends your streak. A wrong answer ends the run.',
        `Reach a streak of ${recipeWinStreak} to clear the run. Finishing or failing starts a 24 hour cooldown.`,
      ],
      round: (round) => `Round ${round}`,
      prompt: 'Which item does this recipe craft?',
      ingredientsLabel: 'Recipe',
      workbenchLabel: 'Requires',
      streak: 'Streak',
      best: 'Best',
      correct: 'Correct',
      incorrect: 'Wrong',
      correctAnswerWas: (name) => `The recipe builds ${name}.`,
      runEnded: 'Run ended',
      cleared: (target) => `Cleared a streak of ${target}.`,
      bestStreak: (best) => `Best streak this run: ${best}.`,
      cooldownActive: 'Come back tomorrow',
      historyLabel: 'This run',
      emptyHistory: 'No rounds played yet.',
      historyLine: (guessed, answer) => `Picked ${guessed}, answer was ${answer}`,
      sourceNote: 'Recipes are taken from the official Facepunch wiki.',
    },
  },
  ru: {
    available: 'Доступно',
    soon: 'скоро',
    now: 'сейчас',
    inMinutes: (minutes) => `через ${minutes} мин`,
    inHoursMinutes: (hours, minutes) => `через ${hours} ч ${minutes} мин`,
    backToMinigames: 'Назад к мини-играм',
    difficulties: {
      easy: 'Легко',
      medium: 'Средне',
      hard: 'Сложно',
    },
    categories: categoriesRu,
    workbench: {
      0: 'Без верстака',
      1: 'Верстак 1 уровня',
      2: 'Верстак 2 уровня',
      3: 'Верстак 3 уровня',
    },
    itemPool: (count) => `${count} предметов`,
    guessItem: {
      title: 'Угадай предмет',
      description: 'Определите предмет Rust по уровню верстака, стоимости крафта и выходу переработки.',
      difficultyLabel: 'Сложность',
      howToPlayTitle: 'Как играть',
      howToPlay: [
        'Вы начинаете с категории предмета. Каждая неверная попытка открывает еще одну подсказку: уровень верстака, затем сколько ресурсов нужно по рецепту, затем самый крупный ингредиент, затем что предмет отдает в переработчике.',
        'Каждая попытка также сравнивается с ответом, поэтому даже неверный предмет подсказывает, выше или ниже уровень верстака у ответа.',
        'На «Легко» — предметы, которые крафтятся без верстака. На «Средне» добавляется верстак 1 уровня. На «Сложно» — весь набор, вплоть до верстака 3 уровня.',
        'Одна загадка на сложность в день. Разгадка или показ ответа запускает 24-часовой перерыв для этой сложности.',
      ],
      puzzleTitle: 'Ежедневная загадка предмета',
      poolNote: (count) => `Ответ — один из ${count} крафтящихся предметов Rust.`,
      attempts: 'Попытки',
      hints: 'Подсказки',
      startingClueLabel: 'Стартовая подсказка',
      startingClue: (category) => `Категория предмета: ${category}`,
      revealedHintsLabel: 'Открытые подсказки',
      hintNumber: (index) => `Подсказка ${index}`,
      hintWorkbenchLabel: 'Уровень верстака',
      hintWorkbenchValue: (workbench) => workbench,
      hintIngredientsLabel: 'Форма рецепта',
      hintIngredientsValue: (distinct, total) =>
        `${distinct} разных ингредиентов, всего ${total} ресурсов.`,
      hintMainIngredientLabel: 'Самый крупный ингредиент',
      hintMainIngredientValue: (quantity, name) => `${quantity} x ${name}`,
      hintRecycleLabel: 'Выход переработки',
      hintRecycleValue: (output) => output,
      hintRecycleUnknown: 'Выход переработки для этого предмета не записан.',
      yourGuess: 'Ваш ответ',
      placeholder: 'Начните вводить название предмета',
      submit: 'Ответить',
      revealItem: 'Показать предмет',
      chooseItem: 'Выберите предмет из списка.',
      alreadyTried: 'Вы уже пробовали этот предмет.',
      historyLabel: 'Прошлые попытки',
      emptyHistory: 'Попыток пока нет.',
      solved: 'Разгадано',
      answerRevealed: 'Ответ показан',
      cooldownActive: 'Возвращайтесь завтра',
      answer: 'Это был предмет',
      revealedAnswer: 'Это был предмет',
      sameCategory: 'Та же категория',
      otherCategory: 'Другая категория',
      sameWorkbench: 'Тот же уровень верстака',
      higherWorkbench: 'Ответу нужен верстак выше',
      lowerWorkbench: 'Ответу нужен верстак ниже',
      sourceNote: 'Рецепты и выход переработки взяты из официальной вики Facepunch.',
    },
    recipeGuess: {
      title: 'Угадай рецепт',
      description: 'Посмотрите рецепт крафта Rust и назовите предмет, который он создает.',
      howToPlayTitle: 'Как играть',
      howToPlay: [
        'Каждый раунд показывает полный рецепт крафта и нужный уровень верстака. Выберите, какой из четырех предметов собирается по этому рецепту.',
        'Неверные варианты берутся с того же уровня верстака, поэтому различает их именно рецепт.',
        'Верный ответ продлевает серию. Неверный завершает забег.',
        `Наберите серию из ${recipeWinStreak}, чтобы пройти забег. Завершение или проигрыш запускает 24-часовой перерыв.`,
      ],
      round: (round) => `Раунд ${round}`,
      prompt: 'Какой предмет создается по этому рецепту?',
      ingredientsLabel: 'Рецепт',
      workbenchLabel: 'Требуется',
      streak: 'Серия',
      best: 'Лучшая',
      correct: 'Верно',
      incorrect: 'Неверно',
      correctAnswerWas: (name) => `По рецепту собирается ${name}.`,
      runEnded: 'Забег завершен',
      cleared: (target) => `Серия из ${target} пройдена.`,
      bestStreak: (best) => `Лучшая серия за забег: ${best}.`,
      cooldownActive: 'Возвращайтесь завтра',
      historyLabel: 'Этот забег',
      emptyHistory: 'Раундов пока не было.',
      historyLine: (guessed, answer) => `Выбрано ${guessed}, ответ — ${answer}`,
      sourceNote: 'Рецепты взяты из официальной вики Facepunch.',
    },
  },
}

export function formatCooldownLabel(
  parts: { state: 'ready' } | { state: 'unknown' } | { state: 'waiting', hours: number, minutes: number },
  copy: CommonCopy,
) {
  if (parts.state === 'unknown') {
    return copy.soon
  }

  if (parts.state === 'ready') {
    return copy.now
  }

  return parts.hours <= 0
    ? copy.inMinutes(parts.minutes)
    : copy.inHoursMinutes(parts.hours, parts.minutes)
}
