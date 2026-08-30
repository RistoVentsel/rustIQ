import { AdSlot } from './AdSlot'
import { type AdPlacement, appConfig } from '../lib/config'

type ContentAdProps = {
  placement: AdPlacement
  /**
   * Pages gate ads with useAdsGate() so nothing serves on empty, loading or
   * error screens. Pass the same condition here so the container is not
   * rendered at all in those states.
   */
  hasContent?: boolean
}

/**
 * A manual ad unit placed inside page content.
 *
 * Renders nothing until the matching VITE_ADSENSE_SLOT_* value is configured,
 * so before ad units exist in the AdSense dashboard the site simply keeps
 * relying on Auto ads. The wrapper is always separated from interactive
 * controls and labelled, so an ad is never mistaken for part of the UI.
 */
export function ContentAd({ placement, hasContent = true }: ContentAdProps) {
  const slotId = appConfig.adSlots[placement]

  if (!slotId || !hasContent) {
    return null
  }

  return (
    <aside className="content-ad" aria-label="Advertisement">
      <span className="content-ad__label">Advertisement</span>
      <AdSlot slotId={slotId} className="content-ad__slot" />
    </aside>
  )
}
