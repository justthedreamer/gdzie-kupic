import { SERVER_EVENTS, type RealtimeEventName, type RealtimePayloads, type RealtimeState } from '~/utils/realtime'

// Keeps one real-time connection alive: starts it, retries when it cannot start, follows
// the automatic reconnects of the underlying connection and starts it again when it closes.
// Framework-free so that it can be unit-tested with a fake connection; the plugin
// (plugins/realtime.client.ts) wires it to SignalR and the realtime store.

/** The part of a SignalR `HubConnection` this client needs. */
export interface RealtimeConnection {
  start: () => Promise<void>
  stop: () => Promise<void>
  on: (method: string, handler: (payload: unknown) => void) => void
  onreconnecting: (callback: () => void) => void
  onreconnected: (callback: () => void) => void
  onclose: (callback: () => void) => void
}

export interface RealtimeSink {
  setState: (state: RealtimeState) => void
  emit: <E extends RealtimeEventName>(name: E, ...payload: RealtimePayloads[E] extends undefined ? [] : [RealtimePayloads[E]]) => void
}

export interface RealtimeClientOptions {
  createConnection: () => RealtimeConnection | Promise<RealtimeConnection>
  sink: RealtimeSink
  /** Delay before the n-th (1-based) failed start / close is retried. */
  retryDelay: (failures: number) => number
}

export function createRealtimeClient({ createConnection, sink, retryDelay }: RealtimeClientOptions) {
  // Every start()/stop() opens a new generation; callbacks of an older one are ignored, so a
  // connection that is being replaced (token change, logout) can never leak state or events.
  let generation = 0
  let connection: RealtimeConnection | null = null
  let timer: ReturnType<typeof setTimeout> | undefined

  function clearTimer() {
    if (timer !== undefined) clearTimeout(timer)
    timer = undefined
  }

  async function attempt(gen: number, conn: RealtimeConnection, failures: number) {
    try {
      await conn.start()
    }
    catch {
      if (gen !== generation) return
      timer = setTimeout(() => void attempt(gen, conn, failures + 1), retryDelay(failures + 1))
      return
    }

    if (gen !== generation) {
      // Stopped while the start was in flight.
      void conn.stop().catch(() => {})
      return
    }

    sink.setState('connected')
    sink.emit('resync')
  }

  async function start() {
    stop()
    const gen = ++generation

    const conn = await createConnection()
    if (gen !== generation) return

    connection = conn

    for (const name of SERVER_EVENTS) {
      conn.on(name, (payload: unknown) => {
        if (gen === generation) (sink.emit as (n: string, p: unknown) => void)(name, payload)
      })
    }

    conn.onreconnecting(() => {
      if (gen === generation) sink.setState('reconnecting')
    })

    conn.onreconnected(() => {
      if (gen !== generation) return
      sink.setState('connected')
      sink.emit('resync')
    })

    // Closed for good (the automatic reconnect gave up or the server dropped us): start again.
    conn.onclose(() => {
      if (gen !== generation) return
      sink.setState('disconnected')
      timer = setTimeout(() => void attempt(gen, conn, 1), retryDelay(1))
    })

    await attempt(gen, conn, 0)
  }

  function stop() {
    generation++
    clearTimer()

    const conn = connection
    connection = null
    if (conn) void conn.stop().catch(() => {})

    sink.setState('disconnected')
  }

  return { start, stop }
}
