import type { PushSubscriptionBody } from '~/utils/push'

// The browser's push API behind one small interface, so that the push store never touches
// `navigator` directly and e2e tests (Playwright cannot receive real pushes) can swap in
// a fake. The fake is installed by plugins/push.client.ts when `public.pushMock` is on.

export interface PushBrowser {
  /** Service worker, Push API and Notification API are all available. */
  isSupported: () => boolean
  permission: () => NotificationPermission
  /** Shows the browser's permission prompt; only call it from a user action. */
  requestPermission: () => Promise<NotificationPermission>
  /** The subscription of this device, or `null`. Rejects when there is no service worker. */
  getSubscription: () => Promise<PushSubscriptionBody | null>
  subscribe: (applicationServerKey: Uint8Array<ArrayBuffer>) => Promise<PushSubscriptionBody>
  /** Drops the subscription of this device; `false` when there was none. */
  unsubscribe: () => Promise<boolean>
}

const READY_TIMEOUT_MS = 10_000

function toBody(subscription: PushSubscription): PushSubscriptionBody {
  const json = subscription.toJSON()

  return {
    endpoint: subscription.endpoint,
    keys: { p256dh: json.keys?.p256dh ?? '', auth: json.keys?.auth ?? '' },
  }
}

// `ready` never settles when no service worker gets registered (e.g. a failed registration).
function activeRegistration(): Promise<ServiceWorkerRegistration> {
  return Promise.race([
    navigator.serviceWorker.ready,
    new Promise<never>((_, reject) => setTimeout(() => reject(new Error('No active service worker')), READY_TIMEOUT_MS)),
  ])
}

export function createBrowserPush(): PushBrowser {
  return {
    isSupported: () =>
      typeof navigator !== 'undefined'
      && 'serviceWorker' in navigator
      && 'PushManager' in window
      && 'Notification' in window,

    permission: () => Notification.permission,

    requestPermission: () => Notification.requestPermission(),

    async getSubscription() {
      const subscription = await (await activeRegistration()).pushManager.getSubscription()

      return subscription ? toBody(subscription) : null
    },

    async subscribe(applicationServerKey) {
      const registration = await activeRegistration()

      return toBody(await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey }))
    },

    async unsubscribe() {
      const subscription = await (await activeRegistration()).pushManager.getSubscription()

      return subscription ? subscription.unsubscribe() : false
    },
  }
}

/**
 * The state of the `pushMock` fake. Tests set these fields (on `window.__push.state`, or in
 * `window.__pushInit` before the app starts) and read the counters to see what the app did.
 */
export interface FakePushState {
  supported: boolean
  permission: NotificationPermission
  /** What the permission prompt answers. */
  promptResult: NotificationPermission
  subscription: PushSubscriptionBody | null
  /** Makes `subscribe` fail (e.g. an unreachable push service). */
  subscribeFails: boolean
  /** How many times the permission prompt was shown / a subscription was created / dropped. */
  prompts: number
  subscribes: number
  unsubscribes: number
  /** The `applicationServerKey` of the last `subscribe`. */
  lastKey: Uint8Array<ArrayBuffer> | null
}

export interface FakePush extends PushBrowser {
  state: FakePushState
}

export function createFakePush(init: Partial<FakePushState> = {}): FakePush {
  const state: FakePushState = {
    supported: true,
    permission: 'default',
    promptResult: 'granted',
    subscription: null,
    subscribeFails: false,
    prompts: 0,
    subscribes: 0,
    unsubscribes: 0,
    lastKey: null,
    ...init,
  }

  return {
    state,

    isSupported: () => state.supported,

    permission: () => state.permission,

    requestPermission() {
      state.prompts++
      state.permission = state.promptResult

      return Promise.resolve(state.permission)
    },

    getSubscription: () => Promise.resolve(state.subscription),

    subscribe(applicationServerKey) {
      if (state.subscribeFails) return Promise.reject(new Error('Push service unreachable'))

      state.subscribes++
      state.lastKey = applicationServerKey
      state.subscription = {
        endpoint: `https://push.example.test/send/${state.subscribes}`,
        keys: { p256dh: 'fake-p256dh', auth: 'fake-auth' },
      }

      return Promise.resolve(state.subscription)
    },

    unsubscribe() {
      const had = state.subscription !== null
      state.subscription = null
      if (had) state.unsubscribes++

      return Promise.resolve(had)
    },
  }
}
