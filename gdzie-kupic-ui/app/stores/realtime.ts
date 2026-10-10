import type { RealtimeEventName, RealtimeHandler, RealtimePayloads, RealtimeState } from '~/utils/realtime'

// The real-time connection as the screens see it: its state and an event bus. The plugin
// (plugins/realtime.client.ts) owns the actual SignalR connection and feeds this store;
// screens never touch the connection - they subscribe with `useRealtimeEvent`.
export const useRealtimeStore = defineStore('realtime', () => {
  const state = ref<RealtimeState>('disconnected')
  const connected = computed(() => state.value === 'connected')

  // Not reactive on purpose: subscriptions are bookkeeping, nothing renders from them.
  const listeners = new Map<RealtimeEventName, Set<RealtimeHandler<RealtimeEventName>>>()

  function setState(next: RealtimeState) {
    state.value = next
  }

  function on<E extends RealtimeEventName>(name: E, handler: RealtimeHandler<E>): () => void {
    const set = listeners.get(name) ?? new Set()
    set.add(handler as RealtimeHandler<RealtimeEventName>)
    listeners.set(name, set)

    return () => {
      set.delete(handler as RealtimeHandler<RealtimeEventName>)
    }
  }

  function emit<E extends RealtimeEventName>(name: E, ...payload: RealtimePayloads[E] extends undefined ? [] : [RealtimePayloads[E]]) {
    // A copy, so a handler may unsubscribe while the event is delivered; one failing
    // handler must not keep the others from running.
    for (const handler of [...(listeners.get(name) ?? [])]) {
      try {
        handler(payload[0] as RealtimePayloads[E])
      }
      catch (err) {
        console.error(`[realtime] a "${name}" handler failed`, err)
      }
    }
  }

  return { state, connected, setState, on, emit }
})
