import { useEffect, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'

import type { ServerDetails, ServerMapMetadataResponse, ServerStatus } from '../../api'
import { appConfig } from '../../lib'

type ServerUpdateMessage = {
  serverId: string
  players: number
  maxPlayers: number
  queuePlayers: number | null
  status: ServerStatus
  checkedAtUtc: string
  updatedSecondsAgo: number
  mapName: string | null
  seed: number | null
  size: number | null
  rustMapsUrl: string | null
  thumbnailUrl: string | null
}

export type LiveConnectionStatus =
  | 'idle'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'error'

function hasMapSummary(update: ServerUpdateMessage) {
  return (
    update.mapName !== null ||
    update.seed !== null ||
    update.size !== null ||
    update.rustMapsUrl !== null ||
    update.thumbnailUrl !== null
  )
}

export function useServerLiveUpdates(serverId?: string) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<LiveConnectionStatus>(serverId ? 'connecting' : 'idle')

  useEffect(() => {
    if (!serverId) {
      setStatus('idle')
      return
    }

    let isActive = true
    const connection = new HubConnectionBuilder()
      .withUrl(appConfig.apiHubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('ServerUpdated', (update: ServerUpdateMessage) => {
      if (!isActive || update.serverId !== serverId) {
        return
      }

      queryClient.setQueryData<ServerDetails | undefined>(['server', serverId], (current) => {
        if (!current) {
          return current
        }

        return {
          ...current,
          players: update.players,
          maxPlayers: update.maxPlayers,
          queuePlayers: update.queuePlayers,
          status: update.status,
          updatedAtUtc: update.checkedAtUtc,
          updatedSecondsAgo: update.updatedSecondsAgo,
          map: update.mapName ?? current.map,
          mapMetadata: hasMapSummary(update)
            ? {
                mapName: update.mapName,
                seed: update.seed,
                size: update.size,
                rustMapsUrl: update.rustMapsUrl,
                thumbnailUrl: update.thumbnailUrl,
              }
            : current.mapMetadata,
        }
      })

      queryClient.setQueryData<ServerMapMetadataResponse | undefined>(
        ['server', serverId, 'map'],
        (current) => {
          if (!current) {
            return current
          }

          return {
            ...current,
            mapName: update.mapName ?? current.mapName,
            seed: update.seed ?? current.seed,
            size: update.size ?? current.size,
            rustMapsUrl: update.rustMapsUrl ?? current.rustMapsUrl,
            thumbnailUrl: update.thumbnailUrl ?? current.thumbnailUrl,
          }
        },
      )
    })

    connection.onreconnecting(() => {
      if (isActive) {
        setStatus('reconnecting')
      }
    })

    connection.onreconnected(async () => {
      if (!isActive) {
        return
      }

      setStatus('connected')
      await connection.invoke('WatchServer', serverId)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['server', serverId] }),
        queryClient.invalidateQueries({ queryKey: ['server', serverId, 'players'] }),
        queryClient.invalidateQueries({ queryKey: ['server', serverId, 'map'] }),
      ])
    })

    connection.onclose(() => {
      if (isActive) {
        setStatus('disconnected')
      }
    })

    async function start() {
      try {
        setStatus('connecting')
        await connection.start()
        await connection.invoke('WatchServer', serverId)

        if (isActive && connection.state === HubConnectionState.Connected) {
          setStatus('connected')
        }
      } catch {
        if (isActive) {
          setStatus('error')
        }
      }
    }

    void start()

    return () => {
      isActive = false

      async function stop() {
        try {
          if (connection.state === HubConnectionState.Connected) {
            await connection.invoke('UnwatchServer', serverId)
          }
        } catch {
          // Ignore shutdown errors during route transitions.
        } finally {
          await connection.stop()
        }
      }

      void stop()
    }
  }, [queryClient, serverId])

  return status
}
