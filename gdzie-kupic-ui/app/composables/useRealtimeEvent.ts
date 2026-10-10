import type { RealtimeEventName, RealtimeHandler } from '~/utils/realtime'

/**
 * Runs `handler` for every `name` event until the surrounding scope ends (the component
 * unmounts, the store is disposed). `resync` is delivered after every successful (re)connect:
 * refetch whatever the screen shows when it arrives.
 */
export function useRealtimeEvent<E extends RealtimeEventName>(name: E, handler: RealtimeHandler<E>) {
  const off = useRealtimeStore().on(name, handler)

  if (getCurrentScope()) onScopeDispose(off)

  return off
}
