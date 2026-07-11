import { getJson } from './http'
import type { PatchNote } from './types'

export function getLatestPatchNotes(signal?: AbortSignal) {
  return getJson<PatchNote[]>('patch-notes', { signal })
}
