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
    id: 'wipe-timing-server-fit',
    label: 'Server choice',
    title: 'Wipe Timing & Server Fit',
    summary: 'How wipe schedules, tags, and game modes change what a server feels like before you join.',
    description: 'A server with the wrong wipe timing can waste a whole session. Read the wipe schedule, tags, and mode first so you know whether you are joining a fresh race, a settled economy, or last-day chaos.',
    readTime: '4 min read',
    rustIqAngle: 'Score servers by wipe age, advertised schedule, tags, and whether the next 24 hours will become endgame chaos.',
    image: '/rust-knowledge-wipe-timing.png',
    visualLabel: 'Server Read',
    visualStat: 'Wipe rhythm',
    keyTakeaways: [
      'The Missile Silo timer points at wipe timing and endgame event timing.',
      'Monthly, weekly, and biweekly tags imply different default rhythms.',
      'Hardcore removes several information and safety systems.',
    ],
    sections: [
      {
        title: 'Wipe signals',
        intro: 'Wipe information is more than a date. It predicts population spikes, progression pressure, and risk.',
        items: [
          {
            title: 'The Missile Silo timer is not flavor text.',
            detail: 'It counts down to the next server wipe and is also used to schedule last-day endgame events like road Bradleys.',
            sourceLabel: 'Wipe timer',
            sourceUrl: 'https://wiki.facepunch.com/rust/server-wipe-timer',
          },
          {
            title: 'Wipe tags imply default timing.',
            detail: 'Monthly, weekly, and biweekly timer formats default around Thursday 19:00 London time unless the server overrides them.',
            sourceLabel: 'Wipe timer',
            sourceUrl: 'https://wiki.facepunch.com/rust/server-wipe-timer',
          },
        ],
      },
      {
        title: 'Server fit',
        intro: 'Tags and game modes explain the rules you are actually buying into.',
        items: [
          {
            title: 'Server tags are compressed intelligence.',
            detail: 'The browser supports wipe schedule, difficulty, PvE, roleplay, training, tutorial, modded framework, and region tags.',
            sourceLabel: 'Server tags',
            sourceUrl: 'https://wiki.facepunch.com/rust/server-browser-tags',
          },
          {
            title: 'Hardcore changes the information game.',
            detail: 'Hardcore removes or limits systems like the map, compass, team system, global chat, Rust+, safe zones, and BP persistence.',
            sourceLabel: 'Gamemodes',
            sourceUrl: 'https://wiki.facepunch.com/rust/server-gamemodes',
          },
        ],
      },
    ],
    sources: [
      { label: 'Server Wipe Timer', url: 'https://wiki.facepunch.com/rust/server-wipe-timer' },
      { label: 'Server Browser Tags', url: 'https://wiki.facepunch.com/rust/server-browser-tags' },
      { label: 'Server Gamemodes', url: 'https://wiki.facepunch.com/rust/server-gamemodes' },
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
    id: 'map-resource-loops',
    label: 'Map sense',
    title: 'Topology, Travel & Quiet Resource Loops',
    summary: 'Read the island by terrain layers, roads, towers, ocean loot, and low-drama farming routes.',
    description: 'The map quietly tells you where resources, travel options, and risk are likely to exist. Knowing those systems helps you build routes that do not depend on winning every monument fight.',
    readTime: '4 min read',
    rustIqAngle: 'Add map-intel filters for low-risk components, ocean routes, freshwater, node-heavy terrain, and fast travel.',
    image: '/rust-knowledge-map-loops.png',
    visualLabel: 'Route Planning',
    visualStat: 'Low-drama loops',
    keyTakeaways: [
      'Topology controls practical resource expectations.',
      'Roads and rails create steady component loops.',
      'Ocean loot can support quieter progression.',
    ],
    sections: [
      {
        title: 'Terrain reads',
        intro: 'Topology is the hidden layer behind a lot of resource logic.',
        items: [
          {
            title: 'Topology predicts resources before you arrive.',
            detail: 'Map layers decide things like fresh water, node spawns, animal roaming, roadside junk, lake food, and swamp sulfur pickups.',
            sourceLabel: 'Topology',
            sourceUrl: 'https://wiki.facepunch.com/rust/Topology',
          },
        ],
      },
      {
        title: 'Routes',
        intro: 'A good route is repeatable, not just lucky.',
        items: [
          {
            title: 'Roads and rails are component arteries.',
            detail: 'Ring roads and railroads branch into monuments, while road junk piles, barrels, crates, and toolboxes create steady component loops.',
            sourceLabel: 'The Map',
            sourceUrl: 'https://wiki.facepunch.com/rust/map',
          },
          {
            title: 'Transmission towers are built-in rotations.',
            detail: 'Transmission towers spawn in sequence and can be used as zipline travel paths across parts of the island.',
            sourceLabel: 'The Map',
            sourceUrl: 'https://wiki.facepunch.com/rust/map',
          },
          {
            title: 'Ocean farming is more than Oil Rig.',
            detail: 'Underwater rocks, shipwreck bottles, and flotsam can provide components away from the most obvious monument fights.',
            sourceLabel: 'The Map',
            sourceUrl: 'https://wiki.facepunch.com/rust/map',
          },
        ],
      },
    ],
    sources: [
      { label: 'Topology', url: 'https://wiki.facepunch.com/rust/Topology' },
      { label: 'The Map', url: 'https://wiki.facepunch.com/rust/map' },
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
    id: 'patch-watch-2026',
    label: 'Patch watch',
    title: '2026 Systems Players May Have Missed',
    summary: 'Recent systems that affect where players live, trade, raid, and group up.',
    description: 'Rust changes fast. This page highlights recent systems worth knowing before judging server choice or wipe strategy.',
    readTime: '4 min read',
    rustIqAngle: 'Give RustIQ a current-patch lane so players can see what changed before committing to a wipe.',
    image: '/rust-knowledge-patch-watch.png',
    visualLabel: 'Patch Watch',
    visualStat: '2026 systems',
    keyTakeaways: [
      'Apartment rooms are convenient but not absolute safety.',
      'Softcore has different gather and raid-window rules.',
      'Clans add structure beyond normal teams.',
    ],
    sections: [
      {
        title: 'Apartment economy',
        intro: 'The Apartment Complex creates quick starts, rent pressure, and new theft angles.',
        items: [
          {
            title: 'Apartments are temporary homes with rent risk.',
            detail: 'The Apartment Complex lets players rent rooms with scrap, but unpaid rent triggers eviction and item seizure.',
            sourceLabel: 'Common Ground',
            sourceUrl: 'https://rust.facepunch.com/news/common-ground',
          },
          {
            title: 'The master key makes apartments unsafe by design.',
            detail: 'A master key can unlock occupied apartment rooms for five minutes, so rented storage is convenient but not absolute safety.',
            sourceLabel: 'Common Ground',
            sourceUrl: 'https://rust.facepunch.com/news/common-ground',
          },
        ],
      },
      {
        title: 'Rules and groups',
        intro: 'The same server label can now hide very different day-to-day rules.',
        items: [
          {
            title: 'Softcore now has real rule differences.',
            detail: 'Softcore gained 2x resource gather and default raid windows from 6 PM to 9 PM server local time.',
            sourceLabel: 'Common Ground',
            sourceUrl: 'https://rust.facepunch.com/news/common-ground',
          },
          {
            title: 'Clans are more than larger teams.',
            detail: 'The clan system adds roles, permissions, clan chat, announcements, map visibility for nearby members, and Clan Tables.',
            sourceLabel: 'Common Ground',
            sourceUrl: 'https://rust.facepunch.com/news/common-ground',
          },
        ],
      },
    ],
    sources: [
      { label: 'Common Ground', url: 'https://rust.facepunch.com/news/common-ground' },
    ],
  },
  {
    id: 'console-server-ops',
    label: 'Power user',
    title: 'Console, Admin & Server-Ops Clues',
    summary: 'Server-side facts that explain wipes, save files, browser trust, and useful console behavior.',
    description: 'These notes are useful for admins, curious players, and RustIQ diagnostics. They explain why server data can be stale, wrong, or intentionally structured.',
    readTime: '4 min read',
    rustIqAngle: 'Use these as source notes for future admin-facing RustIQ diagnostics and server trust signals.',
    image: '/rust-knowledge-server-ops.png',
    visualLabel: 'Server Ops',
    visualStat: 'Trust signals',
    keyTakeaways: [
      'The F1 console can search command names and descriptions.',
      'Blueprint and save state live in separate server files.',
      'Browser location spoofing became harder in 2026.',
    ],
    sections: [
      {
        title: 'Console and files',
        intro: 'Knowing where state lives helps explain wipe behavior.',
        items: [
          {
            title: 'The F1 console can search itself.',
            detail: 'The find command searches commands, variables, and descriptions, which is faster than memorizing exact convar names.',
            sourceLabel: 'Commands',
            sourceUrl: 'https://wiki.facepunch.com/rust/useful_commands',
          },
          {
            title: 'Blueprints and map state are separate server files.',
            detail: 'player.blueprints stores learned blueprints, while save files hold map data, buildings, deployables, and other entities.',
            sourceLabel: 'Server files',
            sourceUrl: 'https://wiki.facepunch.com/rust/Getting-Started_w-Server',
          },
          {
            title: 'Forced wipes advance protocol expectations.',
            detail: 'The wiki warns against renaming old save files to bypass forced wipe behavior because protocol versioning changes.',
            sourceLabel: 'Server files',
            sourceUrl: 'https://wiki.facepunch.com/rust/Getting-Started_w-Server',
          },
        ],
      },
      {
        title: 'Browser trust',
        intro: 'Server browser data is part of server choice, so spoof resistance matters.',
        items: [
          {
            title: 'Server browser location spoofing got harder.',
            detail: 'The May 2026 browser update estimates server country and coordinates from IP data so nearby sorting is harder to fake.',
            sourceLabel: 'Browser update',
            sourceUrl: 'https://rust.facepunch.com/news/upgrade-hard-raid-harder',
          },
        ],
      },
    ],
    sources: [
      { label: 'Useful Console Commands', url: 'https://wiki.facepunch.com/rust/useful_commands' },
      { label: 'Getting started with your Rust server', url: 'https://wiki.facepunch.com/rust/Getting-Started_w-Server' },
      { label: 'Upgrade hard, raid harder', url: 'https://rust.facepunch.com/news/upgrade-hard-raid-harder' },
    ],
  },
]

export function getKnowledgeGuide(topicId: string | undefined) {
  return knowledgeGuides.find((guide) => guide.id === topicId)
}
