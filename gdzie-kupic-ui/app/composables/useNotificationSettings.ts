// The e-mail switch of the notification settings: loads the account setting and saves a change.
// A failed save leaves the switch where it was (the value only changes after the server accepted it).
export function useNotificationSettings() {
  const api = useAccountApi()

  const loading = ref(true)
  const loadFailed = ref(false)
  const saving = ref(false)
  const saveFailed = ref(false)
  const emailEnabled = ref(false)

  async function load() {
    loading.value = true
    loadFailed.value = false

    try {
      emailEnabled.value = (await api.notificationSettings()).emailEnabled
    }
    catch {
      loadFailed.value = true
    }
    finally {
      loading.value = false
    }
  }

  /** Saves the e-mail switch; resolves `true` when the server accepted it. */
  async function setEmailEnabled(enabled: boolean): Promise<boolean> {
    if (saving.value || loading.value || loadFailed.value) return false

    saving.value = true
    saveFailed.value = false

    try {
      await api.updateNotificationSettings({ emailEnabled: enabled })
      emailEnabled.value = enabled

      return true
    }
    catch {
      saveFailed.value = true

      return false
    }
    finally {
      saving.value = false
    }
  }

  return { loading, loadFailed, saving, saveFailed, emailEnabled, load, setEmailEnabled }
}
