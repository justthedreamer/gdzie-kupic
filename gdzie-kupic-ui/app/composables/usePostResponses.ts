import type { MaybeRefOrGetter } from 'vue'
import type { PostResponse } from '~/composables/api/usePostsApi'

/**
 * The merchants that can help with one request. It has no timer of its own: it follows
 * the status polling (`refreshOn` changes with every status refresh, see `usePostStatus`),
 * so the list refreshes on the same schedule and stops with it when the page is left or
 * the request ends. A failed refresh keeps the last known list.
 */
export function usePostResponses(
  id: MaybeRefOrGetter<string | null | undefined>,
  refreshOn: () => unknown,
) {
  const api = usePostsApi()

  const responses = shallowRef<PostResponse[]>([])
  const error = shallowRef<unknown>(null)
  const loaded = ref(false)

  let run = 0

  async function load() {
    const postId = toValue(id)
    if (!postId) return

    const current = ++run

    try {
      const next = await api.responses(postId)
      if (current !== run) return
      responses.value = sortResponses(next)
      error.value = null
    }
    catch (err) {
      if (current !== run) return
      error.value = err
    }

    loaded.value = true
  }

  watch(() => toValue(id), () => {
    run++
    responses.value = []
    error.value = null
    loaded.value = false
  })

  // Fires once the first status load finished (successfully or not), then on every refresh.
  watch(refreshOn, (value) => {
    if (value !== undefined) void load()
  })

  onBeforeUnmount(() => {
    run++
  })

  return { responses, error, loaded, refresh: load }
}
