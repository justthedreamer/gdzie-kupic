import { createRealtimeClient } from '~/utils/realtimeClient'
import {
  realtimeHubUrl,
  reconnectDelay,
  type RealtimeEventName,
  type RealtimeHandler,
  type RealtimeState,
} from '~/utils/realtime'

// Test hook of the `realtimeMock` mode (see docs/api.md § Real-time events): e2e tests play
// the server through `window.__realtime`.
interface RealtimeMockHook {
  getState: () => RealtimeState
  setState: (state: RealtimeState) => void
  emit: (name: RealtimeEventName, payload?: unknown) => void
  on: (name: RealtimeEventName, handler: (payload: unknown) => void) => () => void
}

declare global {
  interface Window {
    __realtime?: RealtimeMockHook
  }
}

function installMock(store: ReturnType<typeof useRealtimeStore>) {
  window.__realtime = {
    getState: () => store.state,
    // Like the real connection, becoming connected asks every subscriber to refetch.
    setState(state) {
      store.setState(state)
      if (state === 'connected') store.emit('resync')
    },
    emit: (name, payload) => (store.emit as (n: RealtimeEventName, p?: unknown) => void)(name, payload),
    on: (name, handler) => store.on(name, handler as RealtimeHandler<RealtimeEventName>),
  }
}

// One SignalR connection per signed-in client (client-only: the auth state lives in the
// browser). It follows the token: opened after sign-in, restarted when the token changes
// (dev account switcher), closed on sign-out. With `public.realtimeMock` there is no
// connection at all - the `window.__realtime` hook stands in for the server.
export default defineNuxtPlugin(() => {
  const config = useRuntimeConfig().public
  const store = useRealtimeStore()

  if (config.realtimeMock) {
    installMock(store)
    return
  }

  const auth = useAuthStore()

  const client = createRealtimeClient({
    sink: store,
    retryDelay: reconnectDelay,
    createConnection: async () => {
      const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr')

      return new HubConnectionBuilder()
        // The token travels in the query string for WebSockets; no cookies are involved.
        .withUrl(realtimeHubUrl(String(config.apiBase)), {
          accessTokenFactory: () => auth.token ?? '',
          withCredentials: false,
        })
        // Retries for ever with a capped backoff; a connection that still closes is
        // started again by the client.
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: ctx => reconnectDelay(ctx.previousRetryCount) })
        .configureLogging(LogLevel.Warning)
        .build()
    },
  })

  watch(
    () => auth.token,
    (token) => {
      if (token) void client.start()
      else client.stop()
    },
    { immediate: true },
  )
})
