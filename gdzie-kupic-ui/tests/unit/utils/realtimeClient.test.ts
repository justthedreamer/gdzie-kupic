import { describe, it, expect, vi, afterEach } from 'vitest'
import { createRealtimeClient, type RealtimeConnection } from '~/utils/realtimeClient'
import type { RealtimeState } from '~/utils/realtime'

class FakeConnection implements RealtimeConnection {
  handlers = new Map<string, (payload: unknown) => void>()
  reconnecting: () => void = () => {}
  reconnected: () => void = () => {}
  closed: () => void = () => {}
  startResults: boolean[] = [] // false = the start fails; empty = succeeds
  start = vi.fn(async () => {
    if (this.startResults.shift() === false) throw new Error('unreachable')
  })

  stop = vi.fn(async () => {})
  on = (method: string, handler: (payload: unknown) => void) => { this.handlers.set(method, handler) }
  onreconnecting = (cb: () => void) => { this.reconnecting = cb }
  onreconnected = (cb: () => void) => { this.reconnected = cb }
  onclose = (cb: () => void) => { this.closed = cb }
}

function setup(connections: FakeConnection[] = [new FakeConnection()]) {
  const states: RealtimeState[] = []
  const events: [string, unknown][] = []
  const queue = [...connections]
  const createConnection = vi.fn(() => queue.shift()!)

  const client = createRealtimeClient({
    createConnection,
    retryDelay: failures => failures * 1000,
    sink: {
      setState: state => states.push(state),
      emit: ((name: string, payload?: unknown) => events.push([name, payload])) as never,
    },
  })

  return { client, states, events, connections, createConnection }
}

describe('createRealtimeClient', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('connects, reports connected and asks for a resync', async () => {
    const { client, states, events, connections } = setup()

    await client.start()

    expect(connections[0]!.start).toHaveBeenCalledTimes(1)
    expect(states.at(-1)).toBe('connected')
    expect(events).toEqual([['resync', undefined]])
  })

  it('forwards the server events with their payload', async () => {
    const { client, events, connections } = setup()
    await client.start()

    connections[0]!.handlers.get('postAdded')!({ postId: 'p1' })
    connections[0]!.handlers.get('notificationRaised')!({ kind: 'newMessage', postId: null, threadId: 't1' })

    expect(events).toContainEqual(['postAdded', { postId: 'p1' }])
    expect(events).toContainEqual(['notificationRaised', { kind: 'newMessage', postId: null, threadId: 't1' }])
  })

  it('reports reconnecting, then connected with a resync after an automatic reconnect', async () => {
    const { client, states, events, connections } = setup()
    await client.start()
    events.length = 0

    connections[0]!.reconnecting()
    expect(states.at(-1)).toBe('reconnecting')

    connections[0]!.reconnected()
    expect(states.at(-1)).toBe('connected')
    expect(events).toEqual([['resync', undefined]])
  })

  it('retries a failed start with a growing delay and resyncs once connected', async () => {
    vi.useFakeTimers()
    const conn = new FakeConnection()
    conn.startResults = [false, false]
    const { client, states, events } = setup([conn])

    await client.start()
    expect(conn.start).toHaveBeenCalledTimes(1)
    expect(states.at(-1)).toBe('disconnected')

    await vi.advanceTimersByTimeAsync(1000) // first retry after 1 s - fails again
    expect(conn.start).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(1999)
    expect(conn.start).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(1) // second retry after 2 s - succeeds
    expect(conn.start).toHaveBeenCalledTimes(3)

    expect(states.at(-1)).toBe('connected')
    expect(events).toEqual([['resync', undefined]])
  })

  it('starts again when the connection closes', async () => {
    vi.useFakeTimers()
    const { client, states, events, connections } = setup()
    await client.start()
    events.length = 0

    connections[0]!.closed()
    expect(states.at(-1)).toBe('disconnected')

    await vi.advanceTimersByTimeAsync(1000)
    expect(connections[0]!.start).toHaveBeenCalledTimes(2)
    expect(states.at(-1)).toBe('connected')
    expect(events).toEqual([['resync', undefined]])
  })

  it('stop() closes the connection, reports disconnected and cancels pending retries', async () => {
    vi.useFakeTimers()
    const conn = new FakeConnection()
    conn.startResults = [false]
    const { client, states } = setup([conn])

    await client.start()
    client.stop()
    await vi.advanceTimersByTimeAsync(60_000)

    expect(conn.start).toHaveBeenCalledTimes(1)
    expect(conn.stop).toHaveBeenCalled()
    expect(states.at(-1)).toBe('disconnected')
  })

  it('start() replaces the previous connection and ignores its late callbacks', async () => {
    const first = new FakeConnection()
    const second = new FakeConnection()
    const { client, states, events } = setup([first, second])

    await client.start()
    await client.start()
    expect(first.stop).toHaveBeenCalled()
    expect(states.at(-1)).toBe('connected')
    events.length = 0

    first.handlers.get('postAdded')!({ postId: 'stale' })
    first.closed()
    first.reconnected()
    second.handlers.get('postAdded')!({ postId: 'fresh' })

    expect(events).toEqual([['postAdded', { postId: 'fresh' }]])
    expect(states.at(-1)).toBe('connected')
  })

  it('does not report connected when stopped while the start was in flight', async () => {
    const conn = new FakeConnection()
    let release: () => void = () => {}
    conn.start = vi.fn(() => new Promise<void>((resolve) => { release = resolve }))
    const { client, states, events } = setup([conn])

    const starting = client.start()
    await Promise.resolve()
    await Promise.resolve()
    client.stop()
    release()
    await starting

    expect(conn.stop).toHaveBeenCalled()
    expect(states).not.toContain('connected')
    expect(events).toEqual([])
  })
})
