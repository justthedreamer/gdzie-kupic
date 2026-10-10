import { parseApiError } from '~/utils/apiError'
import { isNotConfigured, urlBase64ToUint8Array, type PushState } from '~/utils/push'

// Sign-out must not hang on a slow network: after this the registration is left for the
// server to clean up (a dead endpoint is removed on the first failed push).
const SIGN_OUT_TIMEOUT_MS = 3_000

// Push on this device: its state, turning it on and off, and keeping the registration at the
// service in step with the signed-in user (re-sent after every sign-in, removed on sign-out).
// The browser's push API comes from `usePushBrowser()`; the settings screen and the banner
// only read `state` / `permission` and call `enable()` / `disable()` from a user action.
export const usePushStore = defineStore('push', () => {
  const api = usePushApi()

  const state = ref<PushState | 'unknown'>('unknown')
  /** The browser permission (`default` = not decided yet), as of the last check. */
  const permission = ref<NotificationPermission>('default')
  const busy = ref(false)
  /** The last failed `enable()` / `disable()`; reset by the next attempt. */
  const error = shallowRef<unknown>(null)

  let vapidKey: string | null = null

  // The server's VAPID key (cached); throws when it cannot be had.
  async function loadKey(): Promise<string> {
    vapidKey ??= await api.vapidPublicKey()

    return vapidKey
  }

  /** Reads the state of push on this device. */
  async function refresh() {
    const browser = usePushBrowser()

    if (!browser.isSupported()) {
      state.value = 'unsupported'
      return
    }

    permission.value = browser.permission()
    if (permission.value === 'denied') {
      state.value = 'blocked'
      return
    }

    let subscribed: boolean
    try {
      subscribed = (await browser.getSubscription()) !== null
    }
    catch {
      // No active service worker: nothing to subscribe with.
      state.value = 'unsupported'
      return
    }

    if (permission.value === 'granted' && subscribed) {
      state.value = 'on'
      return
    }

    try {
      await loadKey()
      state.value = 'off'
    }
    catch (err) {
      // Any other failure is transient: the switch stays usable and `enable()` tries again.
      state.value = isNotConfigured(parseApiError(err).status) ? 'notConfigured' : 'off'
    }
  }

  /**
   * Turns push on for this device: asks for the permission, subscribes with the server's key and
   * registers the subscription. Call it from a user action (the permission prompt needs one).
   * Resolves `true` when push is on; otherwise `state` tells why, and `error` holds a failure.
   */
  async function enable(): Promise<boolean> {
    if (busy.value) return false

    busy.value = true
    error.value = null

    try {
      const browser = usePushBrowser()

      if (!browser.isSupported()) {
        state.value = 'unsupported'
        return false
      }

      // First, before any request: browsers only show the prompt right after a user action.
      permission.value = await browser.requestPermission()
      if (permission.value !== 'granted') {
        state.value = permission.value === 'denied' ? 'blocked' : 'off'
        return false
      }

      let key: string
      try {
        key = await loadKey()
      }
      catch (err) {
        if (isNotConfigured(parseApiError(err).status)) {
          state.value = 'notConfigured'
          return false
        }
        throw err
      }

      const subscription = (await browser.getSubscription())
        ?? (await browser.subscribe(urlBase64ToUint8Array(key)))

      try {
        await api.register(subscription)
      }
      catch (err) {
        // Not registered at the service: do not keep a subscription that would never be used.
        await browser.unsubscribe().catch(() => false)
        throw err
      }

      state.value = 'on'
      return true
    }
    catch (err) {
      error.value = err
      state.value = 'off'
      return false
    }
    finally {
      busy.value = false
    }
  }

  /** Turns push off for this device: removes the registration, then drops the subscription. */
  async function disable(): Promise<boolean> {
    if (busy.value) return false

    busy.value = true
    error.value = null

    try {
      const browser = usePushBrowser()
      const subscription = await browser.getSubscription()

      if (subscription) {
        await api.remove(subscription.endpoint)
        await browser.unsubscribe()
      }

      state.value = 'off'
      return true
    }
    catch (err) {
      // Still on: the registration could not be removed, so keep the subscription as well.
      error.value = err
      return false
    }
    finally {
      busy.value = false
    }
  }

  /**
   * After every sign-in: while the permission is granted, (re-)registers the current subscription
   * for the user who just signed in. The endpoint may have changed, or the device may have
   * changed owner. Never throws.
   */
  async function syncAfterSignIn() {
    const browser = usePushBrowser()
    if (!browser.isSupported() || browser.permission() !== 'granted') return

    try {
      const subscription = await browser.getSubscription()
      if (subscription) await api.register(subscription)
    }
    catch (err) {
      console.warn('[push] could not register this device', err)
    }
  }

  /**
   * Before sign-out (the request still carries the user's token): removes the registration of
   * this device from the service, so the next user of the device gets no one else's notifications.
   * The browser keeps its subscription. Never throws, never waits longer than a few seconds.
   */
  async function removeOnSignOut() {
    const browser = usePushBrowser()
    if (!browser.isSupported() || browser.permission() !== 'granted') return

    let timer: ReturnType<typeof setTimeout> | undefined

    try {
      await Promise.race([
        (async () => {
          const subscription = await browser.getSubscription()
          if (subscription) await api.remove(subscription.endpoint)
        })(),
        new Promise<void>((resolve) => {
          timer = setTimeout(resolve, SIGN_OUT_TIMEOUT_MS)
        }),
      ])
    }
    catch (err) {
      console.warn('[push] could not remove the registration of this device', err)
    }
    finally {
      clearTimeout(timer)
    }

    state.value = 'unknown'
  }

  return { state, permission, busy, error, refresh, enable, disable, syncAfterSignIn, removeOnSignOut }
})
