// The signed-in user's own first name, shared by the shell (user card), the buyer
// greeting and the account page. The cache is bound to the user id, so switching
// accounts always triggers a fresh lookup.
export const useProfileStore = defineStore('profile', () => {
  const firstName = ref<string | null>(null)
  const loadedFor = ref<string | null>(null)

  function set(name: string | null) {
    firstName.value = name
    loadedFor.value = useAuthStore().user?.id ?? null
  }

  /** Loads the first name once per user; a failure leaves it empty (callers fall back to the e-mail). */
  async function load(force = false): Promise<string | null> {
    const userId = useAuthStore().user?.id ?? null

    if (userId === null) {
      reset()
      return null
    }

    if (!force && loadedFor.value === userId) return firstName.value

    // Never show the previous account's name while the new one loads.
    if (loadedFor.value !== userId) firstName.value = null

    try {
      set((await useAccountApi().get()).firstName)
    }
    catch {
      firstName.value = null
      loadedFor.value = null
    }

    return firstName.value
  }

  /** Saves the name (empty clears it) and returns the stored value. Throws on API errors. */
  async function save(input: string): Promise<string | null> {
    const name = normalizeFirstName(input)
    const profile = await useAccountApi().updateFirstName(name === '' ? null : name)

    set(profile.firstName)

    return firstName.value
  }

  function reset() {
    firstName.value = null
    loadedFor.value = null
  }

  return { firstName, load, save, set, reset }
})
