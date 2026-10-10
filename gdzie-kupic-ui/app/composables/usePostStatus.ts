import type { MaybeRefOrGetter } from 'vue'
import type { PostStatusInfo } from '~/composables/api/usePostsApi'

/**
 * Status of one request, refreshed by polling (5 s while matching is pending,
 * 30 s afterwards) until real-time updates arrive in Phase 6. Polling stops when
 * the component unmounts, and while `enabled` is false (e.g. the request ended)
 * only the initial load runs. A failed refresh keeps the last known status.
 */
export function usePostStatus(
  id: MaybeRefOrGetter<string | null | undefined>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const api = usePostsApi()

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

    timer = setTimeout(load, statusPollInterval(status.value?.notificationDispatchStatus))
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

  watch(() => toValue(id), restart)
  watch(() => toValue(enabled), (on) => {
    if (on) schedule()
    else clear()
  })

  return { status, error, loaded, refresh: load }
}
