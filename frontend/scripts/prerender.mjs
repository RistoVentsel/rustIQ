/**
 * Post-build prerenderer.
 *
 * rustIQ ships as a client-rendered SPA, so before this step every URL served
 * the same ~2KB shell with an empty <div id="root">, an identical <title> and a
 * canonical tag pointing at the homepage. Crawlers that do not execute JS -
 * including the AdSense review crawler - saw a site with no content and exactly
 * one page. That is what "Low value content" was describing.
 *
 * This script writes one real HTML file per static route, containing the same
 * text the React app renders (both read the same data modules), plus per-route
 * title/description/canonical. React's createRoot replaces the markup on the
 * client, so runtime behaviour is unchanged.
 */
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { createServer } from 'vite'

const root = path.resolve(fileURLToPath(new URL('..', import.meta.url)))
const distDir = path.join(root, 'dist')

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

/** Loads the app's TypeScript data modules through Vite's SSR transform. */
async function loadAppModules() {
  const vite = await createServer({
    root,
    logLevel: 'warn',
    server: { middlewareMode: true },
    appType: 'custom',
  })

  try {
    const [seo, knowledge, pages, games, items] = await Promise.all([
      vite.ssrLoadModule('/src/seo/siteMeta.ts'),
      vite.ssrLoadModule('/src/features/knowledge/rustKnowledgeContent.ts'),
      vite.ssrLoadModule('/src/content/sitePages.ts'),
      vite.ssrLoadModule('/src/features/minigames/itemGameCopy.ts'),
      vite.ssrLoadModule('/src/features/minigames/rustItemData.ts'),
    ])

    return { seo, knowledge, pages, games, items }
  } finally {
    await vite.close()
  }
}

// --- content renderers -----------------------------------------------------

function renderStaticPage(copy) {
  const sections = copy.sections
    .map((section) => `<section><h2>${escapeHtml(section.heading)}</h2>${
      section.body.map((paragraph) => `<p>${escapeHtml(paragraph)}</p>`).join('')
    }</section>`)
    .join('')

  return `<article class="prerender">
<p class="page-eyebrow">${escapeHtml(copy.eyebrow)}</p>
<h1>${escapeHtml(copy.heading)}</h1>
${copy.lead ? `<p>${escapeHtml(copy.lead)}</p>` : ''}
<p>${escapeHtml(copy.intro)}</p>
${sections}
</article>`
}

function renderList(items) {
  return `<ul>${items.map((item) => `<li>${item}</li>`).join('')}</ul>`
}

function renderGuideExtras(guide, knowledge) {
  // Two guides render their body from standalone data tables rather than from
  // `sections`, so they need explicit handling or they would prerender empty.
  if (guide.id === 'keybinds') {
    const binds = knowledge.getKeybindCommands('en')
    return `<h2>Console binds</h2>${renderList(binds.map((bind) =>
      `<code>${escapeHtml(bind.command)}</code> &mdash; ${escapeHtml(bind.description)}`))}`
  }

  if (guide.id === 'farming-plant-genetics') {
    const genes = knowledge.getPlantGenes('en')
    const rules = knowledge.getBreedingRules('en')
    const workflow = knowledge.getBreedingWorkflow('en')
    const builds = knowledge.getIdealGeneBuilds('en')

    return [
      `<h2>The six genes</h2>`,
      genes.map((gene) => `<section><h3>${escapeHtml(gene.gene)} &mdash; ${escapeHtml(gene.name)}</h3>`
        + `<p>${escapeHtml(gene.role)}</p>`
        + `<p>${escapeHtml(gene.inPractice)}</p>`
        + `<p>${escapeHtml(gene.commonMistake)}</p></section>`).join(''),
      `<h2>How crossbreeding resolves</h2>`,
      renderList(rules.map((rule) => `<strong>${escapeHtml(rule.title)}</strong> ${escapeHtml(rule.detail)}`)),
      `<h2>Breeding workflow</h2>`,
      renderList(workflow.map((step) => `<strong>${escapeHtml(step.title)}</strong> ${escapeHtml(step.detail)}`)),
      `<h2>Clone targets worth aiming for</h2>`,
      renderList(builds.map((build) => `<strong>${escapeHtml(build.title)}</strong> ${escapeHtml(build.detail)}`)),
    ].join('')
  }

  return ''
}

function renderGuide(guide, knowledge) {
  const takeaways = guide.keyTakeaways?.length
    ? `<h2>Key takeaways</h2>${renderList(guide.keyTakeaways.map(escapeHtml))}`
    : ''

  const sections = (guide.sections ?? [])
    .map((section) => `<section><h2>${escapeHtml(section.title)}</h2>`
      + `<p>${escapeHtml(section.intro)}</p>`
      + section.items.map((item) => `<div><h3>${escapeHtml(item.title)}</h3>`
        + `<p>${escapeHtml(item.detail)}</p>`
        + (item.sourceUrl
          ? `<p><a href="${escapeHtml(item.sourceUrl)}" rel="noopener nofollow">${escapeHtml(item.sourceLabel ?? 'Source')}</a></p>`
          : '')
        + `</div>`).join('')
      + `</section>`)
    .join('')

  const sources = guide.sources?.length
    ? `<h2>Sources</h2>${renderList(guide.sources.map((source) =>
        `<a href="${escapeHtml(source.url)}" rel="noopener nofollow">${escapeHtml(source.label)}</a>`))}`
    : ''

  return `<article class="prerender">
<p class="page-eyebrow">Rust Knowledge</p>
<h1>${escapeHtml(guide.title)}</h1>
<p>${escapeHtml(guide.readTime)}</p>
<p>${escapeHtml(guide.summary)}</p>
<p>${escapeHtml(guide.description)}</p>
${takeaways}
${sections}
${renderGuideExtras(guide, knowledge)}
${sources}
<p><a href="/rust-knowledge">All Rust knowledge guides</a></p>
</article>`
}

function renderKnowledgeHub(guides) {
  return `<article class="prerender">
<p class="page-eyebrow">Rust Knowledge</p>
<h1>Rust guides, genetics, keybinds and progression</h1>
<p>Practical Rust guides written for players who already play the game. Each guide explains the mechanic rather than restating it, and links the official Facepunch source where the behaviour is documented.</p>
<section><h2>Guides</h2>
${guides.map((guide) => `<div>
<h3><a href="/rust-knowledge/${escapeHtml(guide.id)}">${escapeHtml(guide.title)}</a></h3>
<p>${escapeHtml(guide.readTime)}</p>
<p>${escapeHtml(guide.summary)}</p>
<p>${escapeHtml(guide.description)}</p>
</div>`).join('')}
</section>
</article>`
}

function renderHome(guides) {
  return `<article class="prerender">
<h1>rustIQ &mdash; Rust server monitor and population tracker</h1>
<p>rustIQ tracks Rust servers and answers one question quickly: is this server worth joining right now? Search and filter every Rust server rustIQ has discovered, compare live populations, check wipe schedules and maps, then open a server page for a live player count that updates while you watch.</p>
<section><h2>How the server list works</h2>
<p>The list is a cached snapshot rather than a live feed. It refreshes when you open the page, search, change a filter, or move to the next page, which keeps large result sets fast. Population, map and player counts are read from each server over the Steam A2S query protocol, and every successful query is stored as a snapshot to build population history and an uptime record.</p>
<p>A server that goes offline is not removed straight away. A server rustIQ has seen before is marked offline and keeps its history, so you can tell a short outage from a server that is gone. Once it has stopped answering for a week and dropped off the public server lists too, it is removed so the list stays focused on servers you can actually join.</p>
</section>
<section><h2>Live server pages</h2>
<p>Opening a server shows its cached record immediately, then re-queries that single server every few seconds over a live connection. When several people watch the same server, rustIQ queries it once and broadcasts the result, instead of querying once per viewer. A single failed query does not flip a server to offline &mdash; the page shows how long ago it was last seen.</p>
</section>
<section><h2>Rust guides</h2>
<p>rustIQ also publishes written Rust guides covering farming genetics, progression routes, base systems and console keybinds.</p>
${renderList(guides.map((guide) =>
  `<a href="/rust-knowledge/${escapeHtml(guide.id)}">${escapeHtml(guide.title)}</a> &mdash; ${escapeHtml(guide.summary)}`))}
</section>
<p><a href="/about">About rustIQ and where the data comes from</a></p>
</article>`
}

function renderMinigamesHub() {
  return `<article class="prerender">
<h1>Rust minigames</h1>
<p>Short Rust challenges, refreshed daily. Two run on the same live server data the tracker uses: guess which server a population snapshot belongs to, or compare two servers and pick the busier one. Two more run on Rust crafting data: identify an item from its recipe clues, or read a recipe and name what it builds. Progress is stored in your own browser, so there is no account and nothing to sign up for.</p>
<section><h2>Server data games</h2>
${renderList([
  '<a href="/minigames/guess-the-server">Guess the Server</a> &mdash; identify a Rust server from its map, wipe timing and player count.',
  '<a href="/minigames/higher-lower">Higher or Lower</a> &mdash; compare two Rust servers and pick the one with the higher player count.',
])}
</section>
<section><h2>Crafting and item games</h2>
<p>Both item games run on crafting costs, workbench requirements and recycler outputs taken from the official Facepunch wiki.</p>
${renderList([
  '<a href="/minigames/guess-the-item">Guess the Item</a> &mdash; name a Rust item from its category, workbench tier, crafting cost and recycler output.',
  '<a href="/minigames/recipe-guess">Recipe Guess</a> &mdash; read a crafting recipe and pick the item it builds.',
])}
</section>
</article>`
}

function renderMinigame(meta, games, items) {
  // The two item games keep their rules in itemGameCopy.ts, so the prerendered
  // "how to play" text is the exact text the React page renders.
  const gameCopy = meta.path === '/minigames/guess-the-item'
    ? games.itemGameCopy.en.guessItem
    : meta.path === '/minigames/recipe-guess'
      ? games.itemGameCopy.en.recipeGuess
      : null

  const rules = gameCopy
    ? `<section><h2>${escapeHtml(gameCopy.howToPlayTitle)}</h2>${
        renderList(gameCopy.howToPlay.map(escapeHtml))
      }<p>${escapeHtml(gameCopy.sourceNote)} Answers are drawn from a set of ${items.rustItemCount} craftable Rust items.</p></section>`
    : `<p>The puzzle is generated from live rustIQ server data and refreshes daily. Your streak is stored in your own browser, so there is no account and nothing to sign up for.</p>`

  return `<article class="prerender">
<p class="page-eyebrow">Rust Minigames</p>
<h1>${escapeHtml(meta.title.split('|')[0].trim())}</h1>
<p>${escapeHtml(meta.description)}</p>
${rules}
<p><a href="/minigames">All rustIQ minigames</a></p>
</article>`
}

// --- page assembly ---------------------------------------------------------

function buildPage({ template, meta, content, siteUrl }) {
  const canonical = `${siteUrl}${meta.path === '/' ? '/' : meta.path}`
  const head = [
    `<title>${escapeHtml(meta.title)}</title>`,
    `<meta name="description" content="${escapeHtml(meta.description)}" />`,
    meta.omitCanonical ? '' : `<link rel="canonical" href="${escapeHtml(canonical)}" />`,
    meta.noindex ? '<meta name="robots" content="noindex, follow" />' : '',
    '<meta property="og:type" content="website" />',
    `<meta property="og:url" content="${escapeHtml(canonical)}" />`,
    `<meta property="og:title" content="${escapeHtml(meta.title)}" />`,
    `<meta property="og:description" content="${escapeHtml(meta.description)}" />`,
  ].filter(Boolean).join('\n    ')

  // Mirrors AppShell.tsx class-for-class: global.css is already loaded when
  // this markup paints, so the moment before React mounts looks like the app
  // instead of an unstyled document.
  const isServerRoute = meta.path === '/' || meta.path.startsWith('/servers')
  const navLink = (to, label, navMeta, isActive) => `<li><a href="${to}" class="${
    isActive ? 'app-nav__link app-nav__link--active' : 'app-nav__link'}"${
    isActive ? ' aria-current="page"' : ''}><span class="app-nav__label">${label}</span><span class="app-nav__meta">${navMeta}</span></a></li>`

  const shell = `<div class="app-shell">
<header class="app-header"><div class="app-header__inner">
<a class="app-brand" href="/"><img class="app-brand__logo" src="/rustiq-logo.png" alt="rustIQ" /><div><p class="app-eyebrow">rustIQ</p><p class="app-title app-title--static">Server Monitor</p></div></a>
<nav aria-label="Primary"><ul class="app-nav">${[
  navLink('/', 'Servers', 'Live browser', isServerRoute),
  navLink('/minigames', 'Minigames', 'Daily challenges', meta.path.startsWith('/minigames')),
  navLink('/rust-knowledge', 'Rust Knowledge', 'Guides &amp; facts', meta.path.startsWith('/rust-knowledge')),
].join('')}</ul></nav>
<div class="app-controls"></div>
</div></header>
<main class="${isServerRoute ? 'app-main app-main--wide' : 'app-main'}">${content}</main>
<footer class="app-footer">
<nav class="app-footer__links" aria-label="Footer"><a href="/about">About</a><a href="/contact">Contact</a><a href="/rust-knowledge">Rust Knowledge</a><a href="/privacy">Privacy Policy</a></nav>
<p class="app-footer__disclaimer">rustIQ is an independent project and is not affiliated with or endorsed by Facepunch Studios. Rust is a trademark of Facepunch Studios.</p>
</footer>
</div>`

  return template
    .replace(/<!--rustiq:head-->[\s\S]*?<!--\/rustiq:head-->/, head)
    .replace('<!--rustiq:app-->', shell)
}

function buildSitemap(routes, siteUrl) {
  const entries = routes
    .filter((route) => !route.noindex)
    .map((route) => `  <url>
    <loc>${escapeHtml(`${siteUrl}${route.path === '/' ? '/' : route.path}`)}</loc>
    <changefreq>${route.changefreq ?? 'weekly'}</changefreq>
    <priority>${route.priority ?? '0.5'}</priority>
  </url>`)
    .join('\n')

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${entries}
</urlset>
`
}

async function writePage(routePath, html) {
  const dir = routePath === '/' ? distDir : path.join(distDir, routePath)
  await mkdir(dir, { recursive: true })
  await writeFile(path.join(dir, 'index.html'), html, 'utf8')
}

// --- main ------------------------------------------------------------------

const { seo, knowledge, pages, games, items } = await loadAppModules()
const { siteUrl, getRouteMeta } = seo

const template = await readFile(path.join(distDir, 'index.html'), 'utf8')

if (!template.includes('<!--rustiq:head-->') || !template.includes('<!--rustiq:app-->')) {
  throw new Error('dist/index.html is missing the rustiq prerender markers - check index.html')
}

const guides = knowledge.getKnowledgeGuides('en')
const routes = getRouteMeta('en')

const contentFor = (meta) => {
  if (meta.path === '/') return renderHome(guides)
  if (meta.path === '/rust-knowledge') return renderKnowledgeHub(guides)
  if (meta.path === '/minigames') return renderMinigamesHub()
  if (meta.path === '/about') return renderStaticPage(pages.getAboutCopy('en'))
  if (meta.path === '/contact') return renderStaticPage(pages.getContactCopy('en'))
  if (meta.path === '/privacy') return renderStaticPage(pages.getPrivacyCopy('en'))

  if (meta.path.startsWith('/rust-knowledge/')) {
    const id = meta.path.slice('/rust-knowledge/'.length)
    const guide = guides.find((candidate) => candidate.id === id)
    if (!guide) throw new Error(`No knowledge guide found for route ${meta.path}`)
    return renderGuide(guide, knowledge)
  }

  if (meta.path.startsWith('/minigames/')) return renderMinigame(meta, games, items)

  throw new Error(`No prerender content renderer for route ${meta.path}`)
}

for (const meta of routes) {
  await writePage(meta.path, buildPage({
    template,
    meta,
    content: contentFor(meta),
    siteUrl,
  }))
}

// Server detail pages are generated from live query data and there are far too
// many to prerender. They are served this noindex shell so a crawler never
// treats thousands of near-identical auto-generated pages as the site's body of
// content. nginx routes /servers/* here.
await writePage('/servers', buildPage({
  template,
  meta: {
    path: '/servers',
    title: 'Rust Server Details | rustIQ',
    description: 'Live population, map, wipe schedule and uptime history for a single Rust server.',
    noindex: true,
    // One shell serves every /servers/:id URL, so a shared canonical would be
    // wrong. useDocumentMeta() sets the real per-server canonical on the client.
    omitCanonical: true,
  },
  content: `<article class="prerender">
<h1>Rust server details</h1>
<p>This page shows live population, map, wipe schedule and uptime history for a single Rust server, refreshed every few seconds while the page is open.</p>
<p><a href="/">Browse all Rust servers</a></p>
</article>`,
  siteUrl,
}))

await writeFile(path.join(distDir, 'sitemap.xml'), buildSitemap(routes, siteUrl), 'utf8')

const indexed = routes.filter((route) => !route.noindex).length
console.log(`prerendered ${routes.length} routes (${indexed} in sitemap) + /servers noindex shell`)
