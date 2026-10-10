import type { MaybeRefOrGetter } from 'vue'
import type { PostStatusInfo } from '~/composables/api/usePostsApi'

/**
 * Status of one request. It refreshes when the server says it changed (`postStatusChanged`
 * for this request) and after every (re)connect (`resync`); the counts always come from the
 * status endpoint, never from the event. Polling (5 s while matching is pending, 30 s
 * afterwards) is only the fallback and runs while the real-time connection is not up.
 * Polling stops when the component unmounts, and while `enabled` is false (e.g. the
 * request ended) only the initial load and the events run. A failed refresh keeps the
 * last known status.
 */
export function usePostStatus(
  id: MaybeRefOrGetter<string | null | undefined>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const api = usePostsApi()
  const realtime = useRealtimeStore()

  const status = shallowRef<PostStatusInfo | null>(null)
  const error = shallowRef<unknown>(null)
  const loaded = ref(false)

  let timer: ReturnType<typeof setTimeout> | undefined
  let run = 0

  function clear() {
    if (timer !== undefined) clearTimeout(timer)
    timer = undefined
  }

  function schedule() {
    clear()
    if (!toValue(enabled) || !toValue(id)) return

    timer = setTimeout(poll, statusPollInterval(status.value?.notificationDispatchStatus))
  }

  // While connected the events keep the status current: skip the request, keep the timer.
  function poll() {
    if (realtime.connected) schedule()
    else void load()
  }

  async function load() {
    const postId = toValue(id)
    if (!postId) return

    const current = ++run

    try {
      const next = await api.status(postId)
      if (current !== run) return
      status.value = next
      error.value = null
    }
    catch (err) {
      if (current !== run) return
      error.value = err
    }

    loaded.value = true
    schedule()
  }

  function restart() {
    run++
    clear()
    status.value = null
    error.value = null
    loaded.value = false

    if (toValue(id)) void load()
  }

  onMounted(restart)
  onBeforeUnmount(() => {
    run++
    clear()
  })

  useRealtimeEvent('postStatusChanged', (event) => {
    if (event.postId === toValue(id)) void load()
  })
  useRealtimeEvent('resync', () => void load())

  watch(() => toValue(id), restart)
  watch(() => toValue(enabled), (on) => {
    if (on) schedule()
    else clear()
  })

  return { status, error, loaded, refresh: load }
}
