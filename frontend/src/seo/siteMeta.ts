import { getKnowledgeGuides } from '../features/knowledge/rustKnowledgeContent'
import type { Language } from '../i18n/language'

export const siteUrl = 'https://www.rustiq.gg'
export const siteName = 'rustIQ'

export type RouteMeta = {
  /** Path without trailing slash, '/' for the home route. */
  path: string
  title: string
  description: string
  /** Sitemap hints. Omitted routes are still prerendered but not listed. */
  changefreq?: 'daily' | 'weekly' | 'monthly'
  priority?: string
  /** Kept out of the sitemap and marked noindex. */
  noindex?: boolean
}

const minigameRoutes: Array<{ path: string, title: string, description: string, noindex?: boolean }> = [
  {
    path: '/minigames/guess-the-server',
    title: 'Guess the Server | rustIQ Minigames',
    description: 'Guess which Rust server a live population snapshot belongs to, using map, wipe timing and player count as clues.',
  },
  {
    path: '/minigames/higher-lower',
    title: 'Higher or Lower | rustIQ Minigames',
    description: 'Compare two Rust servers and pick the one with the higher player count. A quick way to learn how populations differ across server types.',
  },
  {
    path: '/minigames/guess-the-item',
    title: 'Guess the Item | rustIQ Rust Item Quiz',
    description: 'Identify a Rust item from its category, workbench tier, crafting cost and recycler output. A daily puzzle across three difficulty tiers, built on wiki-verified recipes.',
  },
  {
    path: '/minigames/recipe-guess',
    title: 'Recipe Guess | Rust Crafting Recipe Quiz',
    description: 'See a Rust crafting recipe and name the item it builds. Build a streak across rounds using wiki-verified crafting costs and workbench requirements.',
  },
]

const editorialRoutes: RouteMeta[] = [
  {
    path: '/',
    title: 'rustIQ | Rust Server Monitor & Population Tracker',
    description: 'Track live Rust server populations, wipe schedules, maps and uptime. Search and filter every Rust server rustIQ has seen, then open a server for live player counts.',
    changefreq: 'daily',
    priority: '1.0',
  },
  {
    path: '/rust-knowledge',
    title: 'Rust Knowledge | Guides, Genetics, Keybinds & Progression',
    description: 'Practical Rust guides written for players who already play: plant genetics and crossbreeding, blueprint fragment routes, base systems, console keybinds and the latest patch notes.',
    changefreq: 'weekly',
    priority: '0.9',
  },
  {
    path: '/minigames',
    title: 'Rust Minigames | Daily Server and Item Challenges',
    description: 'Short Rust minigames built on live rustIQ data: guess the server, higher or lower populations, item identification and crafting recipes.',
    changefreq: 'weekly',
    priority: '0.7',
  },
  {
    path: '/about',
    title: 'About rustIQ | What This Rust Server Tracker Does',
    description: 'What rustIQ is, where its Rust server data comes from, how often it refreshes, and what the project deliberately does not track.',
    changefreq: 'monthly',
    priority: '0.5',
  },
  {
    path: '/contact',
    title: 'Contact rustIQ | Corrections, Server Requests & Sponsorship',
    description: 'How to reach rustIQ about incorrect server data, adding a server, guide corrections, sponsorship, or privacy requests.',
    changefreq: 'monthly',
    priority: '0.5',
  },
  {
    path: '/privacy',
    title: 'Privacy Policy | rustIQ',
    description: 'How rustIQ handles data, cookies, local storage and third-party advertising.',
    changefreq: 'monthly',
    priority: '0.3',
  },
]

/** Every crawlable route, including one page per knowledge guide. */
export function getRouteMeta(language: Language = 'en'): RouteMeta[] {
  const guideRoutes: RouteMeta[] = getKnowledgeGuides(language).map((guide) => ({
    path: `/rust-knowledge/${guide.id}`,
    title: `${guide.title} | rustIQ Rust Knowledge`,
    description: guide.description,
    changefreq: guide.id === 'latest-patch-notes' ? 'daily' : 'weekly',
    priority: guide.featured ? '0.9' : '0.8',
  }))

  const minigames: RouteMeta[] = minigameRoutes.map((route) => ({
    ...route,
    changefreq: 'weekly',
    priority: '0.6',
  }))

  return [...editorialRoutes, ...guideRoutes, ...minigames]
}

export function findRouteMeta(pathname: string, language: Language = 'en') {
  const normalized = pathname !== '/' && pathname.endsWith('/')
    ? pathname.slice(0, -1)
    : pathname

  return getRouteMeta(language).find((route) => route.path === normalized)
}
