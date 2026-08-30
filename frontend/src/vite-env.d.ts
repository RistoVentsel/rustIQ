/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_ADSENSE_CLIENT_ID?: string
  /** Manual ad unit slot IDs. Optional: unset slots fall back to Auto ads. */
  readonly VITE_ADSENSE_SLOT_SERVERS?: string
  readonly VITE_ADSENSE_SLOT_KNOWLEDGE?: string
  readonly VITE_ADSENSE_SLOT_MINIGAMES?: string
  readonly VITE_ADSENSE_SLOT_ARTICLE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
