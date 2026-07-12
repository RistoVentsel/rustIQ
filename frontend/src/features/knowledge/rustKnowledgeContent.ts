import type { Language } from '../../i18n/language'

export type KnowledgeSource = {
  label: string
  url: string
}

export type KnowledgeGuideItem = {
  title: string
  detail: string
  sourceLabel?: string
  sourceUrl?: string
}

export type KeybindCommand = {
  command: string
  description: string
}

export type KnowledgeGuideSection = {
  title: string
  intro: string
  items: KnowledgeGuideItem[]
}

export type KnowledgeGuide = {
  id: string
  label: string
  title: string
  summary: string
  description: string
  readTime: string
  rustIqAngle: string
  image: string
  visualLabel: string
  visualStat: string
  featured?: boolean
  keyTakeaways: string[]
  sections: KnowledgeGuideSection[]
  sources: KnowledgeSource[]
}

export const patchNotesGuideId = 'latest-patch-notes'

export type PlantGene = {
  gene: string
  name: string
  tone: 'positive' | 'neutral' | 'negative'
  role: string
  inPractice: string
  commonMistake: string
}

export type PlantGuideItem = {
  title: string
  detail: string
}

export const keybindCommands: KeybindCommand[] = [
  {
    command: 'bind f1 consoletoggle;combatlog',
    description: 'Run combatlog on opening console.',
  },
  {
    command: 'bind x forward;sprint',
    description: 'Auto-run until you manually press forward again.',
  },
  {
    command: 'bind z duck',
    description: 'Toggle crouch until you manually press crouch again.',
  },
  {
    command: 'bind [leftshift+f11] attack',
    description: 'Auto-attack constantly until you manually attack again with LMB.',
  },
  {
    command: 'bind [rightshift+f11] attack;duck',
    description: 'Auto-attack and toggle crouch.',
  },
  {
    command: 'bind mouse1 "+attack2;+input.sensitivity .45;input.sensitivity .2"',
    description: 'Lower sensitivity when ADS.',
  },
  {
    command: 'bind mouse1 +lighttoggle;+attack2',
    description: 'Turn on flashlight or laser when ADS.',
  },
  {
    command: 'bind mousewheelup "craft.add -2072273936 2"',
    description: 'Craft bandages when you scroll up.',
  },
  {
    command: 'bind f10 client.disconnect',
    description: "Disconnects you from the server you're on.",
  },
  {
    command: 'bind 7 chat.say "Hello everyone!"',
    description: 'Sends a message to global chat.',
  },
  {
    command: 'bind 8 chat.teamsay "Hey Team!!"',
    description: 'Sends a message to team chat.',
  },
  {
    command: 'bind h "craft.add -2072273936 1"',
    description: 'Auto-crafts 1 bandage.',
  },
]

export const plantGenes: PlantGene[] = [
  {
    gene: 'G',
    name: 'Growth',
    tone: 'positive',
    role: 'Speeds up the plant lifecycle so the crop reaches useful harvest stages sooner.',
    inPractice: 'More G genes means faster production per real-world hour. This is why GGGYYY is popular for active farmers who can replant and harvest often.',
    commonMistake: 'Do not confuse speed with health. A fast plant still performs badly if light, water, ground, or temperature are poor.',
  },
  {
    gene: 'Y',
    name: 'Yield',
    tone: 'positive',
    role: 'Raises the output you get from the crop and improves clone output when taking clones.',
    inPractice: 'More Y genes means fewer planters and less handling for the same output. GGYYYY is slower than GGGYYY but easier to mass-clone and run at scale.',
    commonMistake: 'Do not chase only Y. A plant that yields more but grows too slowly can lose to a balanced G/Y clone over a wipe.',
  },
  {
    gene: 'H',
    name: 'Hardiness',
    tone: 'positive',
    role: 'Improves resilience against bad growing conditions, especially temperature problems.',
    inPractice: 'H is useful in rough farms, cold areas, bad lighting, or temporary setups. In a controlled indoor farm with 100 percent conditions, H is usually a wasted slot compared with G or Y.',
    commonMistake: 'Do not treat H as a perfect-clone gene by default. It is a safety gene, not a production gene.',
  },
  {
    gene: 'W',
    name: 'Water',
    tone: 'negative',
    role: 'Increases water consumption.',
    inPractice: 'W makes the plant harder to keep in the ideal water range and can overload a farm design that works for cleaner clones.',
    commonMistake: 'One W in a donor can win a gene slot unless you overpower it with multiple matching positive genes in that same slot.',
  },
  {
    gene: 'X',
    name: 'Empty',
    tone: 'neutral',
    role: 'Adds no growth, yield, or hardiness benefit.',
    inPractice: 'X is not as operationally painful as W, but it wastes one of the six slots. Breeding is mostly the process of replacing X and W with G or Y.',
    commonMistake: 'Do not keep an X just because the rest of the clone looks strong. One empty slot is still lost production forever unless you breed it out.',
  },
]

export const breedingRules: PlantGuideItem[] = [
  {
    title: 'Every plant has six fixed gene positions.',
    detail: 'A seed rolls its six genes when planted. A clone keeps the exact six-slot sequence from the parent. Slot order matters: a G in slot 1 cannot fix a bad gene in slot 4.',
  },
  {
    title: 'Crossbreeding happens during the Crossbreeding stage.',
    detail: 'When a plant reaches that stage, it checks adjacent plants of the same species in the same planter. In a large planter, the center slot can be influenced by up to eight neighbors.',
  },
  {
    title: 'Only matching plant types influence each other.',
    detail: 'Hemp affects hemp, pumpkins affect pumpkins, and berry colors are separate. A blue berry will not fix a green berry. Planters also do not crossbreed with neighboring planters.',
  },
  {
    title: 'The target is usually sacrificial.',
    detail: 'The plant you are trying to rewrite is not the valuable clone. Put an expendable plant in the best target slot, then use good clones around it as donors.',
  },
  {
    title: 'Each gene slot is calculated independently.',
    detail: 'For slot 1 the plant compares slot 1 from its donors; for slot 2 it compares slot 2, and so on. Two half-good clones combine only if their good letters are in the right positions.',
  },
  {
    title: 'Negative genes are heavier than single positive genes.',
    detail: 'Community breeding calculators model green genes as lower weight than W or X. In practice, one W can beat one G, but two matching G genes can overpower it.',
  },
  {
    title: 'Different positive letters do not stack together.',
    detail: 'A G and a Y in the same slot are separate candidates, not one combined positive vote. To force a slot, you want repeated matching letters in that exact slot.',
  },
  {
    title: 'Timing protects donor plants.',
    detail: 'Let the target get close to Crossbreeding before planting donor clones. If donors hit Crossbreeding first, they can be rewritten by the target or by each other.',
  },
]

export const breedingWorkflow: PlantGuideItem[] = [
  {
    title: 'Collect a seed pool.',
    detail: 'Start with many seeds for the exact crop you want. Plant them, inspect the six genes, and keep anything with many G/Y slots and few W/X slots.',
  },
  {
    title: 'Clone promising plants.',
    detail: 'Once interaction allows cloning, take clones from plants that have useful genes in useful positions. Track the exact slot sequence.',
  },
  {
    title: 'Choose a target slot.',
    detail: 'Use the center of a large planter when possible because it touches the most neighbors. The target plant can be bad; it exists to be overwritten.',
  },
  {
    title: 'Surround with slot-specific donors.',
    detail: 'Place donors so repeated desired letters line up in the same gene position. You are forcing six separate columns.',
  },
  {
    title: 'Inspect the crossbred target.',
    detail: 'If the target becomes a better sequence, clone it before risking it in more experiments. If it misses, destroy or harvest it and try again.',
  },
  {
    title: 'Scale only after the clone is stable.',
    detail: 'Once you have GGGYYY, GGYYYY, or another clean production clone, stop gambling with the original. Clone it, duplicate it, and keep backups.',
  },
]

export const idealGeneBuilds: PlantGuideItem[] = [
  {
    title: 'GGGYYY',
    detail: 'Best general production target: fast growth and strong yield. Good when you actively harvest and replant.',
  },
  {
    title: 'GGYYYY',
    detail: 'Slightly slower but higher yield and clone output. Good for large farms where labor and clone count matter.',
  },
  {
    title: 'GGGGYY',
    detail: 'Very fast but lower yield. Useful when speed matters more than per-harvest output.',
  },
  {
    title: 'H-inclusive clones',
    detail: 'Useful for hostile temperatures or imperfect temporary farms. Usually not optimal in a fully controlled farm room.',
  },
]

export const knowledgeGuides: KnowledgeGuide[] = [
  {
    id: 'keybinds',
    label: 'Keybinds',
    title: 'Keybinds',
    summary: 'Copy-ready F1 console binds for combatlog, auto-run, crouch, ADS sensitivity, chat, bandage crafting, and disconnects.',
    description: 'To perform these actions, open the Rust F1 console. Press F1 on your keyboard, then paste the command into the blue text box.',
    readTime: '3 min read',
    rustIqAngle: 'Keep practical player utility commands in one copy-ready reference for fast wipe setup.',
    image: '/rust-knowledge-server-ops.png',
    visualLabel: 'F1 Console',
    visualStat: `${keybindCommands.length} binds`,
    keyTakeaways: [
      'Open the F1 console before running any bind command.',
      'Each command below can be copied directly.',
      'Some binds toggle behavior until you manually press the matching action again.',
    ],
    sections: [],
    sources: [
      { label: 'Facepunch Useful Console Commands', url: 'https://wiki.facepunch.com/rust/useful_commands' },
    ],
  },
  {
    id: 'rust-tips-tricks',
    label: 'Tips & tricks',
    title: 'Rust Tips & Tricks',
    summary: 'Micro-tips that save kits, expose doorcampers, clean up visibility, and make looting faster.',
    description: 'A focused list of practical Rust tricks: doorcamper comfort checks, watergun flame denial, loot discipline, lean combat settings, and hover-loot control.',
    readTime: '4 min read',
    rustIqAngle: 'Turn small player habits and settings into a checklist that reduces avoidable deaths and wasted loot time.',
    image: '/rust-knowledge-tips-tricks.png',
    visualLabel: 'Field Tricks',
    visualStat: '7 practical tips',
    keyTakeaways: [
      'Use comfort, water, and settings as small information advantages.',
      'Loot high-value items first and leave before the third party arrives.',
      'Hover loot and low-visual-noise settings matter because seconds matter.',
    ],
    sections: [
      {
        title: 'Base and raid micro-tricks',
        intro: 'Small deployables and DLC tools can create real information or denial value.',
        items: [
          {
            title: 'Put a campfire near your exit.',
            detail: 'A lit Camp Fire near the door can act like a cheap doorcamper check. If you are alone and comfort rises above 50, another player may be close to the exit.',
            sourceLabel: 'Camp Fire',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/campfire',
          },
          {
            title: 'Try a watergun against fire raids.',
            detail: 'If someone is flamethrowing, satcheling, or propane bombing a wooden wall or door, keep a Water Pistol or Water Gun inside and try spraying the flames or fuse from your side.',
            sourceLabel: 'Water Pistol',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/pistol.water',
          },
        ],
      },
      {
        title: 'Loot discipline',
        intro: 'The trick is not looting more; it is leaving with the right value.',
        items: [
          {
            title: 'Take the best items and leave.',
            detail: 'When you kill someone loaded, grab the best kit pieces, weapons, meds, explosives, sulfur, scrap, or rare components first and move. Getting some value out beats dying while sorting every slot.',
            sourceLabel: 'Scrap value',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/scrap',
          },
          {
            title: 'Learn value before you need it.',
            detail: 'Know the priority order before the fight ends: guns and ammo, meds, explosives and raid mats, sulfur, scrap, tech trash, keycards, and compact high-tier components. Do not evaluate every item while exposed.',
            sourceLabel: 'Tech Trash',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/techparts',
          },
        ],
      },
      {
        title: 'Options that remove noise',
        intro: 'These settings are about making fights readable and looting faster.',
        items: [
          {
            title: 'Set FOV to 90.',
            detail: 'Use 90 FOV for more peripheral information. The extra overview is useful when moving through doors, checking flanks, or tracking multiple players.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
          {
            title: 'Disable Head Bob and Hurt Flash.',
            detail: 'Turn off Head Bob to remove camera wobble, especially when damaged. Turn off Hurt Flash so damage feedback does not cover the fight with extra screen noise.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
          {
            title: 'Cut clutter: Max Gibs 0, Creation Effects off, RGB off.',
            detail: 'If you do not want extra debris and effects while upgrading or fighting around builds, set Max Gibs to 0, disable Creation Effects, and turn RGB lighting off. Censorship is preference.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
        ],
      },
      {
        title: 'Controls',
        intro: 'One control bind changes how quickly you clear bodies and boxes.',
        items: [
          {
            title: 'Bind Hover Loot somewhere easy.',
            detail: 'Hover Loot lets you hold the assigned key and drag across item slots to loot faster. Put it on an easy key, not something awkward, because it is one of the highest-impact control settings.',
            sourceLabel: 'Keybinds',
            sourceUrl: 'https://wiki.facepunch.com/rust/Keybinds',
          },
        ],
      },
    ],
    sources: [
      { label: 'Camp Fire', url: 'https://wiki.facepunch.com/rust/item/campfire' },
      { label: 'Campfire comfort tip', url: 'https://www.corrosionhour.com/rust-items/camp-fire/' },
      { label: 'Water Pistol', url: 'https://wiki.facepunch.com/rust/item/pistol.water' },
      { label: 'Watergun extinguish tip', url: 'https://wiki.rustclash.com/item/water-gun' },
      { label: 'Scrap', url: 'https://wiki.facepunch.com/rust/item/scrap' },
      { label: 'Tech Trash', url: 'https://wiki.facepunch.com/rust/item/techparts' },
      { label: 'Settings commands', url: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936' },
      { label: 'Keybinds', url: 'https://wiki.facepunch.com/rust/Keybinds' },
    ],
  },
  {
    id: 'farming-plant-genetics',
    label: 'Farming',
    title: 'Plant Genetics & Crossbreeding',
    summary: 'A readable guide to G, Y, H, W, X, clone targets, and how slot-by-slot breeding actually works.',
    description: 'Rust farming is easiest once you stop treating genes as a mystery. This guide breaks plants into six columns, explains what each letter does, and gives a workflow for turning random seeds into stable production clones.',
    readTime: '8 min read',
    rustIqAngle: 'A Rust clone calculator for checking plant genes, comparing clone targets, and planning better crossbreeds.',
    image: '/rust-knowledge-farming.png',
    visualLabel: 'Clone Lab',
    visualStat: '6 gene slots',
    featured: true,
    keyTakeaways: [
      'G and Y are the main production genes.',
      'H is a stability gene, useful mostly when conditions are imperfect.',
      'W and X are the slots you usually breed out.',
      'Crossbreeding is decided per gene position, not across the whole plant.',
    ],
    sections: [],
    sources: [
      { label: 'RustBreeder Calculator', url: 'https://rustbreeder.com/' },
      { label: 'Facepunch Farming & Genetics', url: 'https://wiki.facepunch.com/rust/Farming' },
      { label: 'Facepunch Farming Basics', url: 'https://wiki.facepunch.com/rust/Farming_Basics' },
      { label: 'Rust Breeder Guide', url: 'https://rustbreeder.com/guide/' },
      { label: 'Corrosion Hour Genetics Guide', url: 'https://www.corrosionhour.com/the-rust-genetics-guide/' },
    ],
  },
  {
    id: 'progression-monuments',
    label: 'Progression',
    title: 'Fragments, Workbenches & Monument Pressure',
    summary: 'Modern progression is less about scrap alone and more about route control.',
    description: 'Blueprint fragments shifted the value of monuments, puzzle rooms, and high-end events. The fastest path up the tech ladder is now partly about choosing the right route at the right time.',
    readTime: '5 min read',
    rustIqAngle: 'Turn monument data into route cards for solos, duos, and groups chasing Tier 2 or Tier 3 progression.',
    image: '/rust-knowledge-progression.png',
    visualLabel: 'Monument Route',
    visualStat: 'T2 / T3 push',
    keyTakeaways: [
      'Workbench Level 2 and 3 progression is tied to fragments.',
      'Green plus blue puzzle routes can be efficient early.',
      'Advanced fragments come from dangerous high-value places and fallback conversion.',
    ],
    sections: [
      {
        title: 'Fragment routes',
        intro: 'Workbench progress now pushes players back into the world instead of letting everyone sit in base with scrap.',
        items: [
          {
            title: 'Workbench Level 2 and 3 progression moved into monuments.',
            detail: 'Level 2 needs Basic Blueprint Fragments and Level 3 needs Advanced Blueprint Fragments, pushing players into puzzle and event routes.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
          {
            title: 'A full green plus blue puzzle chain can shortcut early progress.',
            detail: 'Basic fragments are guaranteed in green puzzle rooms and blue-card puzzle paths, so route order matters more than random looting.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
          {
            title: 'Advanced fragments have a fallback path.',
            detail: 'High-end crates and events are the direct route, but players can also convert Basic fragments into Advanced fragments.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
        ],
      },
      {
        title: 'Monument pressure',
        intro: 'Because fragments matter, the game now punishes certain stale camping patterns.',
        items: [
          {
            title: 'Puzzle camping got a systemic counter.',
            detail: 'Keycard monuments can build severe refresh radiation around puzzle loot areas, so camping too close can ruin the hold.',
            sourceLabel: 'Pivot Or Die',
            sourceUrl: 'https://rust.facepunch.com/news/pivot-or-die',
          },
        ],
      },
    ],
    sources: [
      { label: 'Meta Shift', url: 'https://rust.facepunch.com/news/meta-shift' },
      { label: 'Pivot Or Die', url: 'https://rust.facepunch.com/news/pivot-or-die' },
    ],
  },
  {
    id: 'base-systems-automation',
    label: 'Base ops',
    title: 'Base Systems, Farming & Automation',
    summary: 'The small base systems that decide whether a base stays useful through a wipe.',
    description: 'This page keeps base operations separate from plant genetics: build privilege, farm room design, water, workbench value, and automation readiness.',
    readTime: '4 min read',
    rustIqAngle: 'Build checklists for upkeep risk, farm yield, rare bench upgrades, and automation readiness.',
    image: '/rust-knowledge-base-ops.png',
    visualLabel: 'Base Ops',
    visualStat: 'Upkeep checks',
    keyTakeaways: [
      'Tool Cupboard authorization defines who can build nearby.',
      'Planter groups of 3 or 6 simplify lights and sprinklers.',
      'Workbench upgrades make benches more valuable targets.',
    ],
    sections: [
      {
        title: 'Privilege and farm rooms',
        intro: 'A base system is only good if teammates can use it and it stays supplied.',
        items: [
          {
            title: 'Tool Cupboard auth is a 50 meter promise.',
            detail: 'Authorizing on the cupboard grants building privilege in its radius, and every teammate who should build needs to auth too.',
            sourceLabel: 'Tool Cupboard',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/cupboard.tool',
          },
          {
            title: 'Efficient farms prefer planter groups of 3 or 6.',
            detail: 'Ceiling light and sprinkler mechanics make those layouts easier to keep watered and lit without wasting parts.',
            sourceLabel: 'Farming basics',
            sourceUrl: 'https://wiki.facepunch.com/rust/Farming_Basics',
          },
        ],
      },
      {
        title: 'Bench value',
        intro: 'Your workbench can become more than a crafting station.',
        items: [
          {
            title: 'Workbench upgrades are rare, physical value.',
            detail: 'The 2026 upgrade system adds non-craftable bench upgrades with bonus effects, making the bench itself a loot target.',
            sourceLabel: 'Workbench upgrades',
            sourceUrl: 'https://rust.facepunch.com/news/upgrade-hard-raid-harder',
          },
        ],
      },
    ],
    sources: [
      { label: 'Tool Cupboard', url: 'https://wiki.facepunch.com/rust/item/cupboard.tool' },
      { label: 'Farming Basics', url: 'https://wiki.facepunch.com/rust/Farming_Basics' },
      { label: 'Upgrade hard, raid harder', url: 'https://rust.facepunch.com/news/upgrade-hard-raid-harder' },
    ],
  },
  {
    id: patchNotesGuideId,
    label: 'Patch notes',
    title: 'Latest Rust Patch Notes',
    summary: 'The three newest official Rust update posts from Facepunch.',
    description: 'The newest official Rust patch notes, pulled from the Facepunch Rust news feed.',
    readTime: 'Latest 3',
    rustIqAngle: 'Keep RustIQ current by reading the newest three official Rust update posts from Facepunch.',
    image: '/rust-knowledge-patch-watch.png',
    visualLabel: 'Patch Notes',
    visualStat: 'Latest 3',
    keyTakeaways: [
      'Newest official Rust update posts stay visible here.',
      'When a newer post is published, the oldest of the three drops out.',
      'Each note links back to the Facepunch source.',
    ],
    sections: [],
    sources: [
      { label: 'Facepunch Rust News', url: 'https://rust.facepunch.com/news' },
      { label: 'Facepunch Rust News RSS', url: 'https://rust.facepunch.com/rss/news' },
    ],
  },
]

const keybindCommandsRu: KeybindCommand[] = [
  {
    command: 'bind f1 consoletoggle;combatlog',
    description: 'Запускает combatlog при открытии консоли.',
  },
  {
    command: 'bind x forward;sprint',
    description: 'Автобег до ручного нажатия движения вперед.',
  },
  {
    command: 'bind z duck',
    description: 'Переключает приседание до следующего ручного нажатия.',
  },
  {
    command: 'bind [leftshift+f11] attack',
    description: 'Постоянная автоатака до ручного нажатия ЛКМ.',
  },
  {
    command: 'bind [rightshift+f11] attack;duck',
    description: 'Автоатака и переключение приседания.',
  },
  {
    command: 'bind mouse1 "+attack2;+input.sensitivity .45;input.sensitivity .2"',
    description: 'Снижает чувствительность при прицеливании.',
  },
  {
    command: 'bind mouse1 +lighttoggle;+attack2',
    description: 'Включает фонарь или лазер при прицеливании.',
  },
  {
    command: 'bind mousewheelup "craft.add -2072273936 2"',
    description: 'Крафтит бинты при прокрутке вверх.',
  },
  {
    command: 'bind f10 client.disconnect',
    description: 'Отключает вас от текущего сервера.',
  },
  {
    command: 'bind 7 chat.say "Hello everyone!"',
    description: 'Отправляет сообщение в общий чат.',
  },
  {
    command: 'bind 8 chat.teamsay "Hey Team!!"',
    description: 'Отправляет сообщение в командный чат.',
  },
  {
    command: 'bind h "craft.add -2072273936 1"',
    description: 'Автоматически крафтит 1 бинт.',
  },
]

const plantGenesRu: PlantGene[] = [
  {
    gene: 'G',
    name: 'Рост',
    tone: 'positive',
    role: 'Ускоряет жизненный цикл растения, чтобы урожай быстрее доходил до полезных стадий.',
    inPractice: 'Больше G означает больше продукции за реальный час. Поэтому GGGYYY популярен у активных фермеров, которые часто собирают и пересаживают.',
    commonMistake: 'Не путайте скорость со здоровьем. Быстрое растение все равно будет плохим при слабом свете, воде, почве или температуре.',
  },
  {
    gene: 'Y',
    name: 'Урожайность',
    tone: 'positive',
    role: 'Увеличивает результат сбора и улучшает выход клонов при клонировании.',
    inPractice: 'Больше Y означает меньше грядок и меньше ручной работы для того же объема. GGYYYY медленнее GGGYYY, но удобнее для масштабной фермы.',
    commonMistake: 'Не гонитесь только за Y. Растение с большим урожаем, но слишком медленным ростом, может проиграть сбалансированному G/Y-клону за вайп.',
  },
  {
    gene: 'H',
    name: 'Выносливость',
    tone: 'positive',
    role: 'Повышает устойчивость к плохим условиям роста, особенно к проблемам температуры.',
    inPractice: 'H полезен в холодных местах, временных фермах, плохом освещении или нестабильных условиях. В контролируемой комнате со 100% условиями H обычно хуже G или Y.',
    commonMistake: 'Не считайте H обязательным геном идеального клона. Это страховка, а не производственный ген.',
  },
  {
    gene: 'W',
    name: 'Вода',
    tone: 'negative',
    role: 'Увеличивает расход воды.',
    inPractice: 'W усложняет поддержание идеального диапазона воды и может перегрузить ферму, которая работала с более чистыми клонами.',
    commonMistake: 'Один W у донора может выиграть слот, если не перебить его несколькими одинаковыми положительными генами в этом же слоте.',
  },
  {
    gene: 'X',
    name: 'Пусто',
    tone: 'neutral',
    role: 'Не дает бонусов к росту, урожайности или выносливости.',
    inPractice: 'X не так болезнен, как W, но тратит один из шести слотов. Селекция в основном заменяет X и W на G или Y.',
    commonMistake: 'Не оставляйте X только потому, что остальной клон выглядит сильным. Один пустой слот остается потерянной продукцией, пока вы его не выведете.',
  },
]

const breedingRulesRu: PlantGuideItem[] = [
  {
    title: 'У каждого растения шесть фиксированных позиций генов.',
    detail: 'Семя получает шесть генов при посадке. Клон сохраняет точную последовательность родителя. Порядок важен: G в первом слоте не исправит плохой ген в четвертом.',
  },
  {
    title: 'Скрещивание происходит на стадии Crossbreeding.',
    detail: 'Когда растение достигает этой стадии, оно проверяет соседние растения того же вида в той же грядке. В большой грядке центральный слот может получать влияние до восьми соседей.',
  },
  {
    title: 'Влияют только одинаковые типы растений.',
    detail: 'Конопля влияет на коноплю, тыква на тыкву, а цвета ягод считаются отдельно. Синяя ягода не исправит зеленую. Соседние грядки между собой не скрещиваются.',
  },
  {
    title: 'Целевое растение обычно расходное.',
    detail: 'Растение, которое вы переписываете, не должно быть ценным клоном. Поставьте расходное растение в лучший целевой слот, а хорошие клоны вокруг используйте как доноров.',
  },
  {
    title: 'Каждый слот гена считается отдельно.',
    detail: 'Для первого слота растение сравнивает первый слот доноров, для второго - второй и так далее. Два наполовину хороших клона помогут только если нужные буквы стоят в нужных позициях.',
  },
  {
    title: 'Негативные гены тяжелее одиночных положительных.',
    detail: 'Калькуляторы сообщества обычно считают зеленые гены легче, чем W или X. На практике один W может победить один G, но два одинаковых G могут его перебить.',
  },
  {
    title: 'Разные положительные буквы не складываются вместе.',
    detail: 'G и Y в одном слоте - это разные кандидаты, а не общий положительный голос. Чтобы продавить слот, нужны повторяющиеся одинаковые буквы именно в этой позиции.',
  },
  {
    title: 'Тайминг защищает растения-доноры.',
    detail: 'Дайте цели почти дойти до Crossbreeding перед посадкой доноров. Если доноры дойдут первыми, их может переписать цель или другие доноры.',
  },
]

const breedingWorkflowRu: PlantGuideItem[] = [
  {
    title: 'Соберите пул семян.',
    detail: 'Начните с большого количества семян нужной культуры. Посадите их, проверьте шесть генов и оставьте варианты с большим числом G/Y и малым числом W/X.',
  },
  {
    title: 'Клонируйте перспективные растения.',
    detail: 'Когда взаимодействие позволит, берите клоны с растений, у которых полезные гены стоят в полезных позициях. Записывайте точную последовательность слотов.',
  },
  {
    title: 'Выберите целевой слот.',
    detail: 'По возможности используйте центр большой грядки, потому что он касается большего числа соседей. Цель может быть плохой: она существует, чтобы быть переписанной.',
  },
  {
    title: 'Окружите цель донорами под конкретные слоты.',
    detail: 'Ставьте доноров так, чтобы нужные буквы повторялись в одной и той же позиции гена. Вы продавливаете шесть отдельных колонок.',
  },
  {
    title: 'Проверьте скрещенную цель.',
    detail: 'Если цель стала лучше, клонируйте ее перед новыми экспериментами. Если не получилось, уничтожьте или соберите ее и попробуйте снова.',
  },
  {
    title: 'Масштабируйтесь только после стабильного клона.',
    detail: 'Когда получили GGGYYY, GGYYYY или другой чистый производственный клон, перестаньте рисковать оригиналом. Клонируйте, дублируйте и держите запас.',
  },
]

const idealGeneBuildsRu: PlantGuideItem[] = [
  {
    title: 'GGGYYY',
    detail: 'Лучший общий производственный вариант: быстрый рост и сильная урожайность. Хорошо подходит, если вы активно собираете и пересаживаете.',
  },
  {
    title: 'GGYYYY',
    detail: 'Немного медленнее, но больше урожая и клонов. Хорошо для больших ферм, где важны трудозатраты и количество клонов.',
  },
  {
    title: 'GGGGYY',
    detail: 'Очень быстро, но урожай ниже. Полезно, когда скорость важнее результата за один сбор.',
  },
  {
    title: 'Клоны с H',
    detail: 'Полезны при плохой температуре или временных несовершенных фермах. Обычно не оптимальны в полностью контролируемой фермерской комнате.',
  },
]

const knowledgeGuidesRu: KnowledgeGuide[] = [
  {
    id: 'keybinds',
    label: 'Бинды',
    title: 'Бинды',
    summary: 'Готовые F1-команды для combatlog, автобега, приседания, ADS-чувствительности, чата, крафта бинтов и отключения.',
    description: 'Чтобы выполнить эти действия, откройте консоль Rust через F1. Нажмите F1 на клавиатуре, затем вставьте команду в синее поле.',
    readTime: '3 мин чтения',
    rustIqAngle: 'Держите практичные команды игрока в одном готовом справочнике для быстрого старта вайпа.',
    image: '/rust-knowledge-server-ops.png',
    visualLabel: 'F1 консоль',
    visualStat: `${keybindCommandsRu.length} биндов`,
    keyTakeaways: [
      'Откройте F1-консоль перед запуском любой bind-команды.',
      'Каждую команду ниже можно копировать напрямую.',
      'Некоторые бинды переключают действие, пока вы вручную не нажмете соответствующую кнопку снова.',
    ],
    sections: [],
    sources: [
      { label: 'Facepunch Useful Console Commands', url: 'https://wiki.facepunch.com/rust/useful_commands' },
    ],
  },
  {
    id: 'rust-tips-tricks',
    label: 'Советы',
    title: 'Советы и трюки Rust',
    summary: 'Микро-советы, которые сохраняют киты, выявляют дверных кемперов, улучшают видимость и ускоряют лут.',
    description: 'Короткий список практичных Rust-трюков: проверка дверного кемпера через комфорт, тушение огня водой, дисциплина лута, боевые настройки и Hover Loot.',
    readTime: '4 мин чтения',
    rustIqAngle: 'Превратите небольшие привычки и настройки в чеклист, который уменьшает лишние смерти и потерю времени на лут.',
    image: '/rust-knowledge-tips-tricks.png',
    visualLabel: 'Полевые трюки',
    visualStat: '7 практичных советов',
    keyTakeaways: [
      'Используйте комфорт, воду и настройки как небольшие информационные преимущества.',
      'Сначала забирайте самые ценные предметы и уходите до появления третьей стороны.',
      'Hover loot и чистые визуальные настройки важны, потому что секунды решают.',
    ],
    sections: [
      {
        title: 'Микро-трюки базы и рейда',
        intro: 'Малые размещаемые предметы и DLC-инструменты могут дать реальную информацию или защиту.',
        items: [
          {
            title: 'Поставьте костер рядом с выходом.',
            detail: 'Зажженный Camp Fire у двери может быть дешевой проверкой дверного кемпера. Если вы один, а комфорт растет выше 50, другой игрок может быть рядом с выходом.',
            sourceLabel: 'Camp Fire',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/campfire',
          },
          {
            title: 'Пробуйте водяной пистолет против огненных рейдов.',
            detail: 'Если кто-то рейдит деревянную стену или дверь огнеметом, сачелями или пропановыми баллонами, держите Water Pistol или Water Gun внутри и пробуйте тушить пламя или фитиль со своей стороны.',
            sourceLabel: 'Water Pistol',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/pistol.water',
          },
        ],
      },
      {
        title: 'Дисциплина лута',
        intro: 'Смысл не в том, чтобы лутать больше, а в том, чтобы уйти с правильной ценностью.',
        items: [
          {
            title: 'Заберите лучшее и уходите.',
            detail: 'Когда убили загруженного игрока, сначала хватайте лучшие части кита, оружие, медицину, взрывчатку, серу, скрап или редкие компоненты и двигайтесь. Получить часть ценности лучше, чем умереть, сортируя каждый слот.',
            sourceLabel: 'Scrap value',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/scrap',
          },
          {
            title: 'Выучите ценность заранее.',
            detail: 'Знайте приоритет еще до конца боя: оружие и патроны, медицина, взрывчатка и рейдовые материалы, сера, скрап, tech trash, карточки и компактные high-tier компоненты. Не оценивайте каждый предмет на открытом месте.',
            sourceLabel: 'Tech Trash',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/techparts',
          },
        ],
      },
      {
        title: 'Настройки, убирающие шум',
        intro: 'Эти настройки делают бои читабельнее и лут быстрее.',
        items: [
          {
            title: 'Поставьте FOV на 90.',
            detail: 'Используйте 90 FOV для большего бокового обзора. Это помогает проходить двери, проверять фланги и следить за несколькими игроками.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
          {
            title: 'Отключите Head Bob и Hurt Flash.',
            detail: 'Выключите Head Bob, чтобы убрать тряску камеры, особенно при уроне. Выключите Hurt Flash, чтобы эффекты урона не закрывали бой лишним шумом.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
          {
            title: 'Уберите мусор: Max Gibs 0, Creation Effects off, RGB off.',
            detail: 'Если не нужны лишние обломки и эффекты при улучшении или боях у построек, поставьте Max Gibs 0, отключите Creation Effects и RGB-подсветку. Censorship - дело вкуса.',
            sourceLabel: 'Settings commands',
            sourceUrl: 'https://steamcommunity.com/sharedfiles/filedetails/?id=3180714936',
          },
        ],
      },
      {
        title: 'Управление',
        intro: 'Один bind управления меняет скорость зачистки тел и ящиков.',
        items: [
          {
            title: 'Назначьте Hover Loot на удобную клавишу.',
            detail: 'Hover Loot позволяет удерживать назначенную клавишу и проводить по слотам предметов, чтобы лутать быстрее. Поставьте его на удобную кнопку, потому что это одна из самых важных настроек управления.',
            sourceLabel: 'Keybinds',
            sourceUrl: 'https://wiki.facepunch.com/rust/Keybinds',
          },
        ],
      },
    ],
    sources: knowledgeGuides.find((guide) => guide.id === 'rust-tips-tricks')?.sources ?? [],
  },
  {
    id: 'farming-plant-genetics',
    label: 'Ферма',
    title: 'Генетика растений и скрещивание',
    summary: 'Понятный гайд по G, Y, H, W, X, целевым клонам и тому, как реально работает селекция по слотам.',
    description: 'Фермерство в Rust становится проще, когда гены перестают быть загадкой. Этот гайд разбивает растения на шесть колонок, объясняет каждую букву и дает рабочий путь от случайных семян к стабильным клонам.',
    readTime: '8 мин чтения',
    rustIqAngle: 'Калькулятор клонов Rust помогает проверять гены, сравнивать целевые клоны и планировать лучшие скрещивания.',
    image: '/rust-knowledge-farming.png',
    visualLabel: 'Лаборатория клонов',
    visualStat: '6 слотов генов',
    featured: true,
    keyTakeaways: [
      'G и Y - главные производственные гены.',
      'H - ген стабильности, полезен в основном при несовершенных условиях.',
      'W и X - слоты, которые обычно выводят из клона.',
      'Скрещивание решается по каждой позиции гена, а не по растению целиком.',
    ],
    sections: [],
    sources: knowledgeGuides.find((guide) => guide.id === 'farming-plant-genetics')?.sources ?? [],
  },
  {
    id: 'progression-monuments',
    label: 'Прогресс',
    title: 'Фрагменты, верстаки и давление монументов',
    summary: 'Современный прогресс зависит не только от скрапа, но и от контроля маршрутов.',
    description: 'Blueprint fragments изменили ценность монументов, puzzle rooms и высокоуровневых событий. Самый быстрый путь вверх по ветке технологий теперь зависит от правильного маршрута в правильное время.',
    readTime: '5 мин чтения',
    rustIqAngle: 'Превратите данные монументов в маршрутные карточки для соло, дуо и групп, которые идут к Tier 2 или Tier 3.',
    image: '/rust-knowledge-progression.png',
    visualLabel: 'Маршрут монументов',
    visualStat: 'T2 / T3 рывок',
    keyTakeaways: [
      'Прогресс Workbench Level 2 и 3 связан с фрагментами.',
      'Маршруты с green и blue puzzle могут быть эффективны в начале.',
      'Advanced fragments идут из опасных ценных мест и через запасную конвертацию.',
    ],
    sections: [
      {
        title: 'Маршруты фрагментов',
        intro: 'Прогресс верстаков снова выталкивает игроков в мир вместо сидения на базе со скрапом.',
        items: [
          {
            title: 'Прогресс Workbench Level 2 и 3 переехал в монументы.',
            detail: 'Level 2 требует Basic Blueprint Fragments, а Level 3 - Advanced Blueprint Fragments, что толкает игроков в puzzle- и event-маршруты.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
          {
            title: 'Полная green plus blue puzzle-цепочка ускоряет ранний прогресс.',
            detail: 'Basic fragments гарантированы в green puzzle rooms и blue-card puzzle paths, поэтому порядок маршрута важнее случайного лута.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
          {
            title: 'У Advanced fragments есть запасной путь.',
            detail: 'High-end ящики и события - прямой путь, но игроки также могут конвертировать Basic fragments в Advanced fragments.',
            sourceLabel: 'Meta Shift',
            sourceUrl: 'https://rust.facepunch.com/news/meta-shift',
          },
        ],
      },
      {
        title: 'Давление монументов',
        intro: 'Раз фрагменты важны, игра теперь системно наказывает некоторые старые схемы кемпа.',
        items: [
          {
            title: 'У кемпа puzzle-зон появился системный контрмеханизм.',
            detail: 'Keycard-монументы могут накапливать сильную радиацию вокруг puzzle-лута, поэтому слишком близкий кемп может испортить позицию.',
            sourceLabel: 'Pivot Or Die',
            sourceUrl: 'https://rust.facepunch.com/news/pivot-or-die',
          },
        ],
      },
    ],
    sources: knowledgeGuides.find((guide) => guide.id === 'progression-monuments')?.sources ?? [],
  },
  {
    id: 'base-systems-automation',
    label: 'База',
    title: 'Системы базы, ферма и автоматизация',
    summary: 'Малые системы базы, которые решают, останется ли база полезной весь вайп.',
    description: 'Эта страница отделяет базовые операции от генетики растений: привилегия строительства, фермерская комната, вода, ценность верстака и готовность к автоматизации.',
    readTime: '4 мин чтения',
    rustIqAngle: 'Создавайте чеклисты по риску upkeep, урожайности фермы, редким улучшениям верстака и готовности к автоматизации.',
    image: '/rust-knowledge-base-ops.png',
    visualLabel: 'Операции базы',
    visualStat: 'Проверки upkeep',
    keyTakeaways: [
      'Авторизация Tool Cupboard определяет, кто может строить рядом.',
      'Группы грядок по 3 или 6 упрощают свет и разбрызгиватели.',
      'Улучшения верстака делают сам верстак более ценной целью.',
    ],
    sections: [
      {
        title: 'Привилегия строительства и фермерские комнаты',
        intro: 'Система базы хороша только если тиммейты могут ей пользоваться и она остается снабженной.',
        items: [
          {
            title: 'Авторизация в Tool Cupboard - обещание радиусом 50 метров.',
            detail: 'Авторизация в шкафу дает привилегию строительства в его радиусе, и каждый тиммейт, который должен строить, тоже должен авторизоваться.',
            sourceLabel: 'Tool Cupboard',
            sourceUrl: 'https://wiki.facepunch.com/rust/item/cupboard.tool',
          },
          {
            title: 'Эффективные фермы любят группы грядок по 3 или 6.',
            detail: 'Механики потолочного света и разбрызгивателей делают такие раскладки проще для воды и света без лишних деталей.',
            sourceLabel: 'Farming basics',
            sourceUrl: 'https://wiki.facepunch.com/rust/Farming_Basics',
          },
        ],
      },
      {
        title: 'Ценность верстака',
        intro: 'Ваш верстак может стать больше, чем станцией крафта.',
        items: [
          {
            title: 'Улучшения верстака - редкая физическая ценность.',
            detail: 'Система улучшений 2026 добавляет некрафтовые улучшения верстаков с бонусами, из-за чего сам верстак становится целью для лута.',
            sourceLabel: 'Workbench upgrades',
            sourceUrl: 'https://rust.facepunch.com/news/upgrade-hard-raid-harder',
          },
        ],
      },
    ],
    sources: knowledgeGuides.find((guide) => guide.id === 'base-systems-automation')?.sources ?? [],
  },
  {
    id: patchNotesGuideId,
    label: 'Патчи',
    title: 'Последние патчноуты Rust',
    summary: 'Три самых новых официальных поста обновлений Rust от Facepunch.',
    description: 'Самые новые официальные патчноуты Rust из новостной ленты Facepunch.',
    readTime: 'Последние 3',
    rustIqAngle: 'Держите RustIQ актуальным через три самых новых официальных поста обновлений Rust от Facepunch.',
    image: '/rust-knowledge-patch-watch.png',
    visualLabel: 'Патчноуты',
    visualStat: 'Последние 3',
    keyTakeaways: [
      'Самые новые официальные обновления Rust видны здесь.',
      'Когда выходит новый пост, самый старый из трех исчезает из списка.',
      'Каждая заметка ведет обратно на источник Facepunch.',
    ],
    sections: [],
    sources: knowledgeGuides.find((guide) => guide.id === patchNotesGuideId)?.sources ?? [],
  },
]

export function getKnowledgeGuides(language: Language = 'en') {
  return language === 'ru' ? knowledgeGuidesRu : knowledgeGuides
}

export function getKnowledgeGuide(topicId: string | undefined, language: Language = 'en') {
  return getKnowledgeGuides(language).find((guide) => guide.id === topicId)
}

export function getKeybindCommands(language: Language = 'en') {
  return language === 'ru' ? keybindCommandsRu : keybindCommands
}

export function getPlantGenes(language: Language = 'en') {
  return language === 'ru' ? plantGenesRu : plantGenes
}

export function getBreedingRules(language: Language = 'en') {
  return language === 'ru' ? breedingRulesRu : breedingRules
}

export function getBreedingWorkflow(language: Language = 'en') {
  return language === 'ru' ? breedingWorkflowRu : breedingWorkflow
}

export function getIdealGeneBuilds(language: Language = 'en') {
  return language === 'ru' ? idealGeneBuildsRu : idealGeneBuilds
}
