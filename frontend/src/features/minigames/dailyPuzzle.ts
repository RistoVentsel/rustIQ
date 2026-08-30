/**
 * Shared plumbing for the daily minigames: deterministic puzzle selection from
 * a seed, a UTC day key, localStorage persistence, and the 24h cooldown that
 * runs after a puzzle is finished.
 *
 * Guess the Server and Higher / Lower predate this module and keep their own
 * copies of the same logic inside MinigamesPage.tsx.
 */

export const cooldownMs = 24 * 60 * 60 * 1000

/** FNV-1a. Stable across reloads so a given seed always yields one puzzle. */
export function hashString(value: string) {
  let hash = 2166136261
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index)
    hash = Math.imul(hash, 16777619)
  }

  return Math.abs(hash >>> 0)
}

export function getDailyDateKey() {
  return new Date().toISOString().slice(0, 10)
}

export function readStoredState<T>(storageKey: string): T | null {
  try {
    const rawState = window.localStorage.getItem(storageKey)
    return rawState ? JSON.parse(rawState) as T : null
  } catch {
    // Private mode, disabled storage, or corrupt JSON: start fresh.
    return null
  }
}

export function writeStoredState<T>(storageKey: string, state: T) {
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(state))
  } catch {
    // Storage full or blocked. The game stays playable for this session.
  }
}

export function isCooldownActive(nextAvailableAtUtc: string | undefined, nowMs: number) {
  if (!nextAvailableAtUtc) {
    return false
  }

  const nextMs = Date.parse(nextAvailableAtUtc)
  return !Number.isNaN(nextMs) && nextMs > nowMs
}

export type CooldownParts =
  | { state: 'ready' }
  | { state: 'unknown' }
  | { state: 'waiting', hours: number, minutes: number }

export function getCooldownParts(nextAvailableAtUtc: string | undefined, nowMs: number): CooldownParts {
  if (!nextAvailableAtUtc) {
    return { state: 'unknown' }
  }

  const nextMs = Date.parse(nextAvailableAtUtc)
  if (Number.isNaN(nextMs) || nextMs <= nowMs) {
    return { state: 'ready' }
  }

  const totalMinutes = Math.ceil((nextMs - nowMs) / 60000)

  return {
    state: 'waiting',
    hours: Math.floor(totalMinutes / 60),
    minutes: totalMinutes % 60,
  }
}

/** Picks one entry from `pool` for `seed`, avoiding `excludeId` when possible. */
export function pickBySeed<T>(
  pool: T[],
  seed: string,
  getId: (entry: T) => string,
  excludeId?: string,
) {
  if (pool.length === 0) {
    return null
  }

  const index = hashString(seed) % pool.length
  const picked = pool[index]

  if (pool.length > 1 && excludeId && getId(picked) === excludeId) {
    return pool[(index + 1) % pool.length]
  }

  return picked
}

/**
 * Picks `count` distinct entries for `seed`. Used to build multiple-choice
 * options, so the same seed always produces the same options in the same order.
 */
export function pickDistinctBySeed<T>(
  pool: T[],
  seed: string,
  count: number,
  getId: (entry: T) => string,
) {
  const picked: T[] = []
  const seen = new Set<string>()

  for (let attempt = 0; attempt < pool.length * 3 && picked.length < count; attempt += 1) {
    const candidate = pool[hashString(`${seed}:${attempt}`) % pool.length]
    const id = getId(candidate)

    if (!seen.has(id)) {
      seen.add(id)
      picked.push(candidate)
    }
  }

  return picked
}

/** Deterministic shuffle so answer position is not predictable but is stable. */
export function shuffleBySeed<T>(entries: T[], seed: string) {
  return entries
    .map((entry, index) => ({ entry, order: hashString(`${seed}:order:${index}`) }))
    .sort((left, right) => left.order - right.order)
    .map((wrapped) => wrapped.entry)
}
