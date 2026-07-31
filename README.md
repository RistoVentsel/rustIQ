# rustIQ

## BattleMetrics wipe fallback

Map metadata and population remain sourced from A2S. When A2S does not publish a
usable next-wipe schedule, RustIQ reads the structured upcoming-wipe times from
the server's public BattleMetrics settings page. This fallback does not use an API
token or require a BattleMetrics subscription, and it cannot replace population
or map data.
