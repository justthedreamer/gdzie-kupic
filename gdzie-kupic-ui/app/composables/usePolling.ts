// Runs `task` every `intervalMs` while the component is mounted and the browser tab is
// visible — the stand-in for real-time updates (and its fallback, see below). A run never overlaps with
// the previous one (a slow request delays the next tick instead of piling up), a failing
// task does not stop the polling, and returning to the tab runs the task at once.
// With `immediate` the first run happens on mount instead of after the first interval.
// With `fallbackOnly` the task is skipped while the real-time connection is up (the screen
// is kept current by its events and by the `resync` after a reconnect) and polling takes
// over again as soon as the connection drops.
export function usePolling(
  task: () => unknown,
  intervalMs: number,
  options: { immediate?: boolean, fallbackOnly?: boolean } = {},
) {
  const realtime = options.fallbackOnly ? useRealtimeStore() : null
  let timer: ReturnType<typeof setTimeout> | null = null
  let active = false
  let running = false

  const isHidden = () => typeof document !== 'undefined' && document.visibilityState === 'hidden'

  function clear() {
    if (timer !== null) clearTimeout(timer)
    timer = null
  }

  function schedule() {
    if (active && timer === null) timer = setTimeout(tick, intervalMs)
  }

  async function tick() {
    clear()
    if (!active) return

    if (!isHidden() && !running && !realtime?.connected) {
      running = true
      try {
        await task()
      }
      catch {
        // The next tick tries again; the task reports its own errors.
      }
      finally {
        running = false
      }
    }
    schedule()
  }

  function onVisibilityChange() {
    if (active && !isHidden()) void tick()
  }

  function start() {
    if (active) return
    active = true
    document.addEventListener('visibilitychange', onVisibilityChange)

    if (options.immediate) void tick()
    else schedule()
  }

  function stop() {
    active = false
    clear()
    document.removeEventListener('visibilitychange', onVisibilityChange)
  }

  onMounted(start)
  onBeforeUnmount(stop)

  return { start, stop }
}
