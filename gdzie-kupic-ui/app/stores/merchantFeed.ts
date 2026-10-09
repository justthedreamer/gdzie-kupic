// Holds the Merchant Requests Feed so that the feed page and the request
// details page share one list, and a response given on either is visible on
// both. The cache is bound to the user id: switching accounts reloads.
export const useMerchantFeedStore = defineStore('merchantFeed', () => {
  const requests = ref<MerchantFeedRequest[]>([])
  const status = ref<'idle' | 'pending' | 'success' | 'error'>('idle')
  const loadedFor = ref<string | null>(null)

  async function load(force = false): Promise<void> {
    const userId = useAuthStore().user?.id ?? null

    if (!force && status.value === 'success' && loadedFor.value === userId) return

    status.value = 'pending'

    try {
      requests.value = await useMerchantFeedApi().load()
      loadedFor.value = userId
      status.value = 'success'
    }
    catch {
      status.value = 'error'
    }
  }

  function byId(id: string): MerchantFeedRequest | undefined {
    return requests.value.find(request => request.id === id)
  }

  /** Sets (or changes) the merchant's answer to a request. */
  async function respond(id: string, state: MerchantResponse): Promise<void> {
    await useMerchantFeedApi().respond(id, state)

    const request = byId(id)
    if (request) request.myResponse = state
  }

  function reset() {
    requests.value = []
    status.value = 'idle'
    loadedFor.value = null
  }

  return { requests, status, load, byId, respond, reset }
})
