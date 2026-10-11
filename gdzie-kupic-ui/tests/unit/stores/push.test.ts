import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { createPinia, setActivePinia } from 'pinia'
import { usePushStore } from '~/stores/push'
import { createFakePush, type FakePush } from '~/utils/pushBrowser'

const { api, holder } = vi.hoisted(() => ({
  api: { vapidPublicKey: vi.fn(), register: vi.fn(), remove: vi.fn() },
  holder: { browser: null as unknown },
}))

mockNuxtImport('usePushApi', () => () => api)
mockNuxtImport('usePushBrowser', () => () => holder.browser)

// A real VAPID public key: 65 bytes, URL-safe base64.
const KEY = 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM'

const existing = { endpoint: 'https://push.example.test/existing', keys: { p256dh: 'p', auth: 'a' } }
const apiError = (status: number) => Object.assign(new Error(`HTTP ${status}`), { response: { status } })

let browser: FakePush

beforeEach(() => {
  setActivePinia(createPinia())
  browser = createFakePush()
  holder.browser = browser
  api.vapidPublicKey.mockReset().mockResolvedValue(KEY)
  api.register.mockReset().mockResolvedValue(undefined)
  api.remove.mockReset().mockResolvedValue(undefined)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('push store - state', () => {
  it('is unknown until it is read', () => {
    expect(usePushStore().state).toBe('unknown')
  })

  it('is unsupported where the browser cannot do push', async () => {
    browser.state.supported = false
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('unsupported')
    expect(api.vapidPublicKey).not.toHaveBeenCalled()
  })

  it('is unsupported when there is no active service worker', async () => {
    vi.spyOn(browser, 'getSubscription').mockRejectedValue(new Error('No active service worker'))
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('unsupported')
  })

  it('is blocked when the permission was denied', async () => {
    browser.state.permission = 'denied'
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('blocked')
    expect(store.permission).toBe('denied')
  })

  it('is on when the permission is granted and the device is subscribed', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('on')
  })

  it('is off when the permission is undecided, and keeps it as such', async () => {
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('off')
    expect(store.permission).toBe('default')
  })

  it('is off when the permission is granted but there is no subscription', async () => {
    browser.state.permission = 'granted'
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('off')
  })

  it('is notConfigured when the server has no VAPID keys', async () => {
    api.vapidPublicKey.mockRejectedValue(apiError(503))
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('notConfigured')
  })

  it('stays off (usable) after a failure that says nothing about the configuration', async () => {
    api.vapidPublicKey.mockRejectedValue(apiError(500))
    const store = usePushStore()

    await store.refresh()

    expect(store.state).toBe('off')
  })
})

describe('push store - enable', () => {
  it('asks for the permission first, then subscribes with the server key and registers the device', async () => {
    const order: string[] = []
    vi.spyOn(browser, 'requestPermission').mockImplementation(() => {
      order.push('permission')
      browser.state.permission = 'granted'
      return Promise.resolve('granted')
    })
    api.vapidPublicKey.mockImplementation(() => {
      order.push('key')
      return Promise.resolve(KEY)
    })
    const store = usePushStore()

    expect(await store.enable()).toBe(true)

    expect(order).toEqual(['permission', 'key'])
    expect(browser.state.lastKey?.length).toBe(65)
    expect(api.register).toHaveBeenCalledWith(browser.state.subscription)
    expect(store.state).toBe('on')
    expect(store.error).toBeNull()
  })

  it('registers the subscription the device already has instead of creating another', async () => {
    browser.state.subscription = existing
    const store = usePushStore()

    await store.enable()

    expect(browser.state.subscribes).toBe(0)
    expect(api.register).toHaveBeenCalledWith(existing)
  })

  it('is blocked when the user denies the prompt, without a request to the server', async () => {
    browser.state.promptResult = 'denied'
    const store = usePushStore()

    expect(await store.enable()).toBe(false)

    expect(store.state).toBe('blocked')
    expect(api.vapidPublicKey).not.toHaveBeenCalled()
    expect(api.register).not.toHaveBeenCalled()
  })

  it('stays off when the prompt is dismissed', async () => {
    browser.state.promptResult = 'default'
    const store = usePushStore()

    expect(await store.enable()).toBe(false)

    expect(store.state).toBe('off')
    expect(store.permission).toBe('default')
  })

  it('reports a server without VAPID keys and subscribes to nothing', async () => {
    api.vapidPublicKey.mockRejectedValue(apiError(503))
    const store = usePushStore()

    expect(await store.enable()).toBe(false)

    expect(store.state).toBe('notConfigured')
    expect(store.error).toBeNull()
    expect(browser.state.subscribes).toBe(0)
  })

  it('does not keep a subscription the service refused', async () => {
    api.register.mockRejectedValue(apiError(500))
    const store = usePushStore()

    expect(await store.enable()).toBe(false)

    expect(store.state).toBe('off')
    expect(store.error).toBeTruthy()
    expect(browser.state.subscription).toBeNull()
  })

  it('keeps the error of a failed subscription', async () => {
    browser.state.subscribeFails = true
    const store = usePushStore()

    expect(await store.enable()).toBe(false)

    expect(store.state).toBe('off')
    expect(store.error).toBeTruthy()
    expect(api.register).not.toHaveBeenCalled()
  })

  it('ignores a second call while one is running', async () => {
    const store = usePushStore()

    const first = store.enable()
    expect(await store.enable()).toBe(false)
    await first

    expect(browser.state.prompts).toBe(1)
  })
})

describe('push store - disable', () => {
  beforeEach(() => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing
  })

  it('removes the registration and drops the subscription', async () => {
    const store = usePushStore()
    await store.refresh()

    expect(await store.disable()).toBe(true)

    expect(api.remove).toHaveBeenCalledWith(existing.endpoint)
    expect(browser.state.subscription).toBeNull()
    expect(store.state).toBe('off')
  })

  it('stays on, keeping the subscription, when the registration cannot be removed', async () => {
    api.remove.mockRejectedValue(apiError(500))
    const store = usePushStore()
    await store.refresh()

    expect(await store.disable()).toBe(false)

    expect(store.state).toBe('on')
    expect(store.error).toBeTruthy()
    expect(browser.state.subscription).toEqual(existing)
  })
})

describe('push store - registration lifecycle', () => {
  it('re-registers the current subscription after sign-in while the permission is granted', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing

    await usePushStore().syncAfterSignIn()

    expect(api.register).toHaveBeenCalledWith(existing)
  })

  it.each([
    ['the permission is undecided', 'default'],
    ['the permission is denied', 'denied'],
  ] as const)('does nothing after sign-in when %s', async (_name, permission) => {
    browser.state.permission = permission
    browser.state.subscription = existing

    await usePushStore().syncAfterSignIn()

    expect(api.register).not.toHaveBeenCalled()
  })

  it('does nothing after sign-in without a subscription, and where push is unsupported', async () => {
    browser.state.permission = 'granted'
    await usePushStore().syncAfterSignIn()

    browser.state.supported = false
    await usePushStore().syncAfterSignIn()

    expect(api.register).not.toHaveBeenCalled()
  })

  it('never throws when the registration fails', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing
    api.register.mockRejectedValue(apiError(500))
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    await expect(usePushStore().syncAfterSignIn()).resolves.toBeUndefined()

    expect(warn).toHaveBeenCalled()
  })

  it('removes the registration on sign-out but keeps the subscription of the browser', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing
    const store = usePushStore()
    await store.refresh()

    await store.removeOnSignOut()

    expect(api.remove).toHaveBeenCalledWith(existing.endpoint)
    expect(browser.state.subscription).toEqual(existing)
    expect(store.state).toBe('unknown')
  })

  it('does nothing on sign-out when push was never turned on', async () => {
    browser.state.subscription = existing

    await usePushStore().removeOnSignOut()

    expect(api.remove).not.toHaveBeenCalled()
  })

  it('never throws on sign-out when the service fails', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = existing
    api.remove.mockRejectedValue(apiError(500))
    vi.spyOn(console, 'warn').mockImplementation(() => {})

    await expect(usePushStore().removeOnSignOut()).resolves.toBeUndefined()
  })

  it('does not hold the sign-out for longer than a few seconds', async () => {
    vi.useFakeTimers()
    browser.state.permission = 'granted'
    browser.state.subscription = existing
    api.remove.mockReturnValue(new Promise(() => {}))

    const done = vi.fn()
    void usePushStore().removeOnSignOut().then(done)

    await vi.advanceTimersByTimeAsync(2_900)
    expect(done).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(200)
    expect(done).toHaveBeenCalled()
  })
})
