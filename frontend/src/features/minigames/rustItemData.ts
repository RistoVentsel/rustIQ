/**
 * Rust item data used by the Guess the Item and Recipe Guess minigames.
 *
 * Every recipe, workbench level and recycle output below was read from the
 * official Facepunch wiki item page linked in `wikiUrl`. Rust rebalances
 * crafting costs between updates, so when a guide or game answer looks wrong,
 * re-check the wiki page and update the entry here rather than patching the
 * game logic.
 *
 * `recycle` lists only the guaranteed (100% chance) standard-recycler output.
 * The wiki also lists probabilistic bonus returns; those are deliberately left
 * out so a clue can never be wrong for a player who gets an unlucky roll.
 */

export type RustItemCategory =
  | 'weapon'
  | 'tool'
  | 'construction'
  | 'deployable'
  | 'medical'
  | 'explosive'

export type RustItemIngredient = {
  name: string
  quantity: number
}

export type RustItem = {
  /** Rust short name, also the Facepunch wiki slug. */
  id: string
  name: string
  category: RustItemCategory
  /** 0 means craftable with no workbench. */
  workbenchLevel: 0 | 1 | 2 | 3
  ingredients: RustItemIngredient[]
  /** Guaranteed standard-recycler output. Omitted where the wiki is ambiguous. */
  recycle?: RustItemIngredient[]
  wikiUrl: string
}

const wiki = (slug: string) => `https://wiki.facepunch.com/rust/item/${slug}`

export const rustItems: RustItem[] = [
  // --- No workbench ---------------------------------------------------------
  {
    id: 'campfire',
    name: 'Camp Fire',
    category: 'deployable',
    workbenchLevel: 0,
    ingredients: [{ name: 'Wood', quantity: 100 }],
    recycle: [{ name: 'Wood', quantity: 60 }],
    wikiUrl: wiki('campfire'),
  },
  {
    id: 'furnace',
    name: 'Furnace',
    category: 'deployable',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Stones', quantity: 200 },
      { name: 'Wood', quantity: 100 },
      { name: 'Low Grade Fuel', quantity: 50 },
    ],
    recycle: [
      { name: 'Stones', quantity: 120 },
      { name: 'Wood', quantity: 60 },
      { name: 'Low Grade Fuel', quantity: 30 },
    ],
    wikiUrl: wiki('furnace'),
  },
  {
    id: 'bow.hunting',
    name: 'Hunting Bow',
    category: 'weapon',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Wood', quantity: 200 },
      { name: 'Cloth', quantity: 50 },
    ],
    recycle: [
      { name: 'Wood', quantity: 120 },
      { name: 'Cloth', quantity: 30 },
    ],
    wikiUrl: wiki('bow.hunting'),
  },
  {
    id: 'pistol.eoka',
    name: 'Eoka Pistol',
    category: 'weapon',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Wood', quantity: 75 },
      { name: 'Metal Fragments', quantity: 30 },
    ],
    recycle: [
      { name: 'Wood', quantity: 45 },
      { name: 'Metal Fragments', quantity: 18 },
    ],
    wikiUrl: wiki('pistol.eoka'),
  },
  {
    id: 'door.hinged.wood',
    name: 'Wooden Door',
    category: 'construction',
    workbenchLevel: 0,
    ingredients: [{ name: 'Wood', quantity: 300 }],
    recycle: [{ name: 'Wood', quantity: 180 }],
    wikiUrl: wiki('door.hinged.wood'),
  },
  {
    id: 'door.hinged.metal',
    name: 'Sheet Metal Door',
    category: 'construction',
    workbenchLevel: 0,
    ingredients: [{ name: 'Metal Fragments', quantity: 150 }],
    recycle: [{ name: 'Metal Fragments', quantity: 90 }],
    wikiUrl: wiki('door.hinged.metal'),
  },
  {
    id: 'bandage',
    name: 'Bandage',
    category: 'medical',
    workbenchLevel: 0,
    ingredients: [{ name: 'Cloth', quantity: 4 }],
    recycle: [{ name: 'Cloth', quantity: 2 }],
    wikiUrl: wiki('bandage'),
  },
  {
    id: 'box.wooden.large',
    name: 'Large Wood Box',
    category: 'deployable',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Wood', quantity: 250 },
      { name: 'Metal Fragments', quantity: 50 },
    ],
    recycle: [
      { name: 'Wood', quantity: 150 },
      { name: 'Metal Fragments', quantity: 30 },
    ],
    wikiUrl: wiki('box.wooden.large'),
  },
  {
    id: 'machete',
    name: 'Machete',
    category: 'weapon',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Wood', quantity: 100 },
      { name: 'Metal Fragments', quantity: 40 },
    ],
    recycle: [
      { name: 'Wood', quantity: 60 },
      { name: 'Metal Fragments', quantity: 24 },
    ],
    wikiUrl: wiki('machete'),
  },
  {
    id: 'workbench1',
    name: 'Workbench Level 1',
    category: 'deployable',
    workbenchLevel: 0,
    ingredients: [
      { name: 'Wood', quantity: 500 },
      { name: 'Metal Fragments', quantity: 100 },
    ],
    recycle: [
      { name: 'Wood', quantity: 300 },
      { name: 'Metal Fragments', quantity: 60 },
    ],
    wikiUrl: wiki('workbench1'),
  },
  {
    id: 'sleepingbag',
    name: 'Sleeping Bag',
    category: 'deployable',
    workbenchLevel: 0,
    ingredients: [{ name: 'Cloth', quantity: 30 }],
    recycle: [{ name: 'Cloth', quantity: 18 }],
    wikiUrl: wiki('sleepingbag'),
  },

  // --- Workbench Level 1 ----------------------------------------------------
  {
    id: 'hatchet',
    name: 'Hatchet',
    category: 'tool',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Wood', quantity: 100 },
      { name: 'Metal Fragments', quantity: 75 },
    ],
    recycle: [
      { name: 'Wood', quantity: 60 },
      { name: 'Metal Fragments', quantity: 45 },
    ],
    wikiUrl: wiki('hatchet'),
  },
  {
    id: 'pickaxe',
    name: 'Pickaxe',
    category: 'tool',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Metal Fragments', quantity: 125 },
      { name: 'Wood', quantity: 100 },
    ],
    recycle: [
      { name: 'Metal Fragments', quantity: 75 },
      { name: 'Wood', quantity: 60 },
    ],
    wikiUrl: wiki('pickaxe'),
  },
  {
    id: 'pistol.revolver',
    name: 'Revolver',
    category: 'weapon',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Metal Fragments', quantity: 125 },
      { name: 'Cloth', quantity: 25 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    recycle: [
      { name: 'Metal Fragments', quantity: 75 },
      { name: 'Cloth', quantity: 15 },
    ],
    wikiUrl: wiki('pistol.revolver'),
  },
  {
    id: 'shotgun.waterpipe',
    name: 'Waterpipe Shotgun',
    category: 'weapon',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Wood', quantity: 150 },
      { name: 'Metal Fragments', quantity: 75 },
    ],
    recycle: [
      { name: 'Wood', quantity: 90 },
      { name: 'Metal Fragments', quantity: 45 },
    ],
    wikiUrl: wiki('shotgun.waterpipe'),
  },
  {
    id: 'explosive.satchel',
    name: 'Satchel Charge',
    category: 'explosive',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Beancan Grenade', quantity: 4 },
      { name: 'Rope', quantity: 1 },
      { name: 'Small Stash', quantity: 1 },
    ],
    recycle: [{ name: 'Beancan Grenade', quantity: 2 }],
    wikiUrl: wiki('explosive.satchel'),
  },
  {
    id: 'hammer.salvaged',
    name: 'Salvaged Hammer',
    category: 'tool',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Metal Fragments', quantity: 50 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    recycle: [{ name: 'Metal Fragments', quantity: 30 }],
    wikiUrl: wiki('hammer.salvaged'),
  },
  {
    id: 'ladder.wooden.wall',
    name: 'Wooden Ladder',
    category: 'construction',
    workbenchLevel: 1,
    ingredients: [
      { name: 'Wood', quantity: 300 },
      { name: 'Rope', quantity: 3 },
    ],
    recycle: [
      { name: 'Wood', quantity: 180 },
      { name: 'Rope', quantity: 1 },
    ],
    wikiUrl: wiki('ladder.wooden.wall'),
  },

  // --- Workbench Level 2 ----------------------------------------------------
  {
    id: 'syringe.medical',
    name: 'Medical Syringe',
    category: 'medical',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Cloth', quantity: 15 },
      { name: 'Low Grade Fuel', quantity: 10 },
      { name: 'Metal Fragments', quantity: 10 },
    ],
    recycle: [
      { name: 'Cloth', quantity: 9 },
      { name: 'Low Grade Fuel', quantity: 6 },
      { name: 'Metal Fragments', quantity: 6 },
    ],
    wikiUrl: wiki('syringe.medical'),
  },
  {
    id: 'largemedkit',
    name: 'Large Medkit',
    category: 'medical',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Low Grade Fuel', quantity: 10 },
      { name: 'Medical Syringe', quantity: 2 },
    ],
    recycle: [
      { name: 'Low Grade Fuel', quantity: 6 },
      { name: 'Medical Syringe', quantity: 1 },
    ],
    wikiUrl: wiki('largemedkit'),
  },
  {
    id: 'shotgun.pump',
    name: 'Pump Shotgun',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'High Quality Metal', quantity: 15 },
      { name: 'Metal Pipe', quantity: 2 },
      { name: 'Metal Spring', quantity: 1 },
    ],
    recycle: [
      { name: 'High Quality Metal', quantity: 9 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    wikiUrl: wiki('shotgun.pump'),
  },
  {
    id: 'rifle.semiauto',
    name: 'Semi-Automatic Rifle',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Metal Fragments', quantity: 450 },
      { name: 'High Quality Metal', quantity: 4 },
      { name: 'Metal Spring', quantity: 1 },
      { name: 'Semi Automatic Body', quantity: 1 },
    ],
    recycle: [
      { name: 'Metal Fragments', quantity: 270 },
      { name: 'High Quality Metal', quantity: 2 },
    ],
    wikiUrl: wiki('rifle.semiauto'),
  },
  {
    id: 'pistol.semiauto',
    name: 'Semi-Automatic Pistol',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'High Quality Metal', quantity: 4 },
      { name: 'Metal Pipe', quantity: 1 },
      { name: 'Semi Automatic Body', quantity: 1 },
    ],
    recycle: [{ name: 'High Quality Metal', quantity: 2 }],
    wikiUrl: wiki('pistol.semiauto'),
  },
  {
    id: 'smg.thompson',
    name: 'Thompson',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Wood', quantity: 100 },
      { name: 'High Quality Metal', quantity: 10 },
      { name: 'Metal Spring', quantity: 1 },
      { name: 'SMG Body', quantity: 1 },
    ],
    recycle: [
      { name: 'Wood', quantity: 60 },
      { name: 'High Quality Metal', quantity: 6 },
    ],
    wikiUrl: wiki('smg.thompson'),
  },
  {
    id: 'smg.2',
    name: 'Custom SMG',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'High Quality Metal', quantity: 8 },
      { name: 'Metal Spring', quantity: 1 },
      { name: 'SMG Body', quantity: 1 },
    ],
    recycle: [{ name: 'High Quality Metal', quantity: 4 }],
    wikiUrl: wiki('smg.2'),
  },
  {
    id: 'pistol.python',
    name: 'Python Revolver',
    category: 'weapon',
    workbenchLevel: 2,
    ingredients: [
      { name: 'High Quality Metal', quantity: 10 },
      { name: 'Metal Pipe', quantity: 3 },
      { name: 'Metal Spring', quantity: 1 },
    ],
    recycle: [
      { name: 'High Quality Metal', quantity: 6 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    wikiUrl: wiki('pistol.python'),
  },
  {
    id: 'axe.salvaged',
    name: 'Salvaged Axe',
    category: 'tool',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Metal Blade', quantity: 5 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    recycle: [{ name: 'Metal Blade', quantity: 3 }],
    wikiUrl: wiki('axe.salvaged'),
  },
  {
    id: 'wall.frame.garagedoor',
    name: 'Garage Door',
    category: 'construction',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Metal Fragments', quantity: 300 },
      { name: 'Gears', quantity: 2 },
    ],
    recycle: [
      { name: 'Metal Fragments', quantity: 180 },
      { name: 'Gears', quantity: 1 },
    ],
    wikiUrl: wiki('wall.frame.garagedoor'),
  },
  {
    id: 'wall.external.high.stone',
    name: 'High External Stone Wall',
    category: 'construction',
    workbenchLevel: 2,
    ingredients: [
      { name: 'Stones', quantity: 4000 },
      { name: 'Sheet Metal', quantity: 1 },
    ],
    wikiUrl: wiki('wall.external.high.stone'),
  },

  // --- Workbench Level 3 ----------------------------------------------------
  {
    id: 'rifle.ak',
    name: 'Assault Rifle',
    category: 'weapon',
    workbenchLevel: 3,
    ingredients: [
      { name: 'Wood', quantity: 200 },
      { name: 'High Quality Metal', quantity: 50 },
      { name: 'Metal Spring', quantity: 4 },
      { name: 'Rifle Body', quantity: 1 },
    ],
    recycle: [
      { name: 'Wood', quantity: 120 },
      { name: 'High Quality Metal', quantity: 30 },
      { name: 'Metal Spring', quantity: 2 },
    ],
    wikiUrl: wiki('rifle.ak'),
  },
  {
    id: 'rifle.bolt',
    name: 'Bolt Action Rifle',
    category: 'weapon',
    workbenchLevel: 3,
    ingredients: [
      { name: 'High Quality Metal', quantity: 20 },
      { name: 'Metal Pipe', quantity: 3 },
      { name: 'Metal Spring', quantity: 1 },
      { name: 'Rifle Body', quantity: 1 },
    ],
    recycle: [
      { name: 'High Quality Metal', quantity: 12 },
      { name: 'Metal Pipe', quantity: 1 },
    ],
    wikiUrl: wiki('rifle.bolt'),
  },
  {
    id: 'door.hinged.toptier',
    name: 'Armored Door',
    category: 'construction',
    workbenchLevel: 3,
    ingredients: [
      { name: 'High Quality Metal', quantity: 20 },
      { name: 'Gears', quantity: 5 },
    ],
    recycle: [
      { name: 'High Quality Metal', quantity: 12 },
      { name: 'Gears', quantity: 3 },
    ],
    wikiUrl: wiki('door.hinged.toptier'),
  },
  {
    id: 'explosive.timed',
    name: 'Timed Explosive Charge',
    category: 'explosive',
    workbenchLevel: 3,
    ingredients: [
      { name: 'Explosives', quantity: 20 },
      { name: 'Cloth', quantity: 5 },
      { name: 'Tech Trash', quantity: 2 },
    ],
    recycle: [
      { name: 'Explosives', quantity: 12 },
      { name: 'Cloth', quantity: 3 },
      { name: 'Tech Trash', quantity: 1 },
    ],
    wikiUrl: wiki('explosive.timed'),
  },
]

export const rustItemCount = rustItems.length

export function getRustItem(id: string) {
  return rustItems.find((item) => item.id === id)
}

/** Items usable as puzzle answers, ordered so selection is stable across loads. */
export function getPuzzleItems(maxWorkbenchLevel: 0 | 1 | 2 | 3) {
  return rustItems.filter((item) => item.workbenchLevel <= maxWorkbenchLevel)
}

export function formatIngredient(ingredient: RustItemIngredient) {
  return `${ingredient.quantity} x ${ingredient.name}`
}

export function getTotalIngredientCount(item: RustItem) {
  return item.ingredients.reduce((total, ingredient) => total + ingredient.quantity, 0)
}

/** True when the item needs a component that only spawns in loot. */
export function requiresLootComponent(item: RustItem) {
  const craftableResources = new Set([
    'Wood',
    'Stones',
    'Cloth',
    'Metal Fragments',
    'Low Grade Fuel',
    'High Quality Metal',
    'Explosives',
  ])

  return item.ingredients.some((ingredient) => !craftableResources.has(ingredient.name))
}
