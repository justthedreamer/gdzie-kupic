// Caches the signed-in merchant's profile so that route guards and pages do
// not refetch `GET /api/merchant/me` on every navigation. The cache is bound
// to the user id, so switching accounts always triggers a fresh lookup.
export const useMerchantStore = defineStore('merchant', () => {
  const profile = ref<MerchantProfile | null>(null)
  const loadedFor = ref<string | null>(null)

  function setProfile(newProfile: MerchantProfile | null) {
    profile.value = newProfile
    loadedFor.value = useAuthStore().user?.id ?? null
  }

  /**
   * Resolves the merchant profile, or `null` when the merchant has not
   * onboarded yet (404). Other errors are rethrown.
   */
  async function load(force = false): Promise<MerchantProfile | null> {
    const userId = useAuthStore().user?.id ?? null

    if (!force && userId !== null && loadedFor.value === userId) {
      return profile.value
    }

    try {
      setProfile(await useMerchantApi().getMe())
    }
    catch (err) {
      if (parseApiError(err).status !== 404) throw err
      setProfile(null)
    }

    return profile.value
  }

  function reset() {
    profile.value = null
    loadedFor.value = null
  }

  return { profile, load, setProfile, reset }
})
