import { useCallback, useEffect, useMemo, useState } from 'react'

const favoriteServersStorageKey = 'rustiq.favoriteServerIds'

function readFavoriteServerIds() {
  if (typeof window === 'undefined') {
    return []
  }

  try {
    const storedValue = window.localStorage.getItem(favoriteServersStorageKey)
    if (!storedValue) {
      return []
    }

    const parsedValue = JSON.parse(storedValue)
    return Array.isArray(parsedValue)
      ? parsedValue.filter((value): value is string => typeof value === 'string')
      : []
  } catch {
    return []
  }
}

function writeFavoriteServerIds(serverIds: string[]) {
  window.localStorage.setItem(favoriteServersStorageKey, JSON.stringify(serverIds))
}

export function useFavoriteServers() {
  const [favoriteServerIds, setFavoriteServerIds] = useState<string[]>(readFavoriteServerIds)

  useEffect(() => {
    function syncFavorites() {
      setFavoriteServerIds(readFavoriteServerIds())
    }

    window.addEventListener('storage', syncFavorites)

    return () => window.removeEventListener('storage', syncFavorites)
  }, [])

  const favoriteServerIdSet = useMemo(() => new Set(favoriteServerIds), [favoriteServerIds])

  const toggleFavoriteServer = useCallback((serverId: string) => {
    setFavoriteServerIds((currentServerIds) => {
      const nextServerIds = currentServerIds.includes(serverId)
        ? currentServerIds.filter((currentServerId) => currentServerId !== serverId)
        : [...currentServerIds, serverId]

      writeFavoriteServerIds(nextServerIds)
      return nextServerIds
    })
  }, [])

  return {
    favoriteServerIds,
    favoriteServerIdSet,
    toggleFavoriteServer,
  }
}
