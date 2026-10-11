import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { usePushStore } from '~/stores/push'
import { PUSH_BANNER_DISMISSED_KEY } from '~/utils/notificationSettings'
import { createFakePush, type FakePush } from '~/utils/pushBrowser'
import NotificationSettingsPage from '~/pages/settings/notifications.vue'
import PushBanner from '~/components/push/Banner.vue'

const { accountApi, pushApi, holder } = vi.hoisted(() => ({
  accountApi: { notificationSettings: vi.fn(), updateNotificationSettings: vi.fn() },
  pushApi: { vapidPublicKey: vi.fn(), register: vi.fn(), remove: vi.fn() },
  holder: { browser: null as unknown },
}))

mockNuxtImport('useAccountApi', () => () => accountApi)
mockNuxtImport('usePushApi', () => () => pushApi)
mockNuxtImport('usePushBrowser', () => () => holder.browser)

const KEY = 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM'
const subscription = { endpoint: 'https://push.example.test/1', keys: { p256dh: 'p', auth: 'a' } }

let browser: FakePush
const mounted: Array<{ unmount: () => void }> = []

async function mountPage(role: 'Buyer' | 'Merchant' = 'Buyer') {
  useAuthStore().setAuth('token', { id: 'user-1', email: 'a@test', role })
  const wrapper = await mountSuspended(NotificationSettingsPage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

async function mountBanner() {
  const wrapper = await mountSuspended(PushBanner)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const pushSwitch = (w: Awaited<ReturnType<typeof mountPage>>) => w.get('[data-testid="push-switch"]')
const emailSwitch = (w: Awaited<ReturnType<typeof mountPage>>) => w.get('[data-testid="email-switch"]')

beforeEach(() => {
  // A fresh store each time: it caches the VAPID key and the app's Pinia outlives the tests.
  const previous = usePushStore()
  previous.$patch({ state: 'unknown', permission: 'default', busy: false, error: null })
  previous.$dispose()
  localStorage.clear()
  browser = createFakePush()
  holder.browser = browser
  pushApi.vapidPublicKey.mockReset().mockResolvedValue(KEY)
  pushApi.register.mockReset().mockResolvedValue(undefined)
  pushApi.remove.mockReset().mockResolvedValue(undefined)
  accountApi.notificationSettings.mockReset().mockResolvedValue({ emailEnabled: false })
  accountApi.updateNotificationSettings.mockReset().mockResolvedValue(undefined)
})

afterEach(() => {
  mounted.splice(0).forEach(wrapper => wrapper.unmount())
})

describe('notification settings page - push switch', () => {
  it('is off and usable when push is available but not enabled', async () => {
    const wrapper = await mountPage()

    expect(pushSwitch(wrapper).attributes('aria-checked')).toBe('false')
    expect(pushSwitch(wrapper).attributes('disabled')).toBeUndefined()
    expect(wrapper.get('[data-testid="push-hint"]').text()).toContain('The browser will ask for permission')
  })

  it('is on when this device is subscribed', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = subscription

    const wrapper = await mountPage()

    expect(pushSwitch(wrapper).attributes('aria-checked')).toBe('true')
  })

  it('is disabled with an explanation where push is not supported', async () => {
    browser.state.supported = false

    const wrapper = await mountPage()

    expect(pushSwitch(wrapper).attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="push-hint"]').text()).toContain('home screen')
  })

  it('is disabled with an explanation when the browser blocks notifications', async () => {
    browser.state.permission = 'denied'

    const wrapper = await mountPage()

    expect(pushSwitch(wrapper).attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="push-hint"]').text()).toContain('blocked in the browser')
  })

  it('is disabled when push is not configured on the server', async () => {
    pushApi.vapidPublicKey.mockRejectedValue(Object.assign(new Error('503'), { response: { status: 503 } }))

    const wrapper = await mountPage()

    expect(pushSwitch(wrapper).attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="push-hint"]').text()).toContain('not available right now')
  })

  it('registers this device when switched on', async () => {
    const wrapper = await mountPage()

    await pushSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(browser.state.prompts).toBe(1)
    expect(pushApi.register).toHaveBeenCalledOnce()
    expect(pushSwitch(wrapper).attributes('aria-checked')).toBe('true')
  })

  it('removes this device when switched off', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = subscription
    const wrapper = await mountPage()

    await pushSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(pushApi.remove).toHaveBeenCalledWith(subscription.endpoint)
    expect(pushSwitch(wrapper).attributes('aria-checked')).toBe('false')
  })

  it('shows an error and stays off when registering fails', async () => {
    pushApi.register.mockRejectedValue(new Error('boom'))
    const wrapper = await mountPage()

    await pushSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="push-error"]').exists()).toBe(true)
    expect(pushSwitch(wrapper).attributes('aria-checked')).toBe('false')
  })

  it('turns into the blocked explanation when the user refuses the browser prompt', async () => {
    browser.state.promptResult = 'denied'
    const wrapper = await mountPage()

    await pushSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(pushSwitch(wrapper).attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="push-hint"]').text()).toContain('blocked in the browser')
  })
})

describe('notification settings page - e-mail switch', () => {
  it('shows the saved setting', async () => {
    accountApi.notificationSettings.mockResolvedValue({ emailEnabled: true })

    const wrapper = await mountPage()

    expect(emailSwitch(wrapper).attributes('aria-checked')).toBe('true')
  })

  it('words the hint for a buyer and for a merchant', async () => {
    const buyer = await mountPage('Buyer')
    expect(buyer.get('[data-testid="email-hint"]').text()).toContain('merchant responses')

    const merchant = await mountPage('Merchant')
    expect(merchant.get('[data-testid="email-hint"]').text()).toContain('regular summary')
  })

  it('saves a change', async () => {
    const wrapper = await mountPage()

    await emailSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(accountApi.updateNotificationSettings).toHaveBeenCalledWith({ emailEnabled: true })
    expect(emailSwitch(wrapper).attributes('aria-checked')).toBe('true')
  })

  it('keeps the previous state and shows an error when saving fails', async () => {
    accountApi.updateNotificationSettings.mockRejectedValue(new Error('boom'))
    const wrapper = await mountPage()

    await emailSwitch(wrapper).trigger('click')
    await flushPromises()

    expect(emailSwitch(wrapper).attributes('aria-checked')).toBe('false')
    expect(wrapper.find('[data-testid="email-save-error"]').exists()).toBe(true)
  })

  it('shows an error with a retry when the setting cannot be loaded', async () => {
    accountApi.notificationSettings.mockRejectedValueOnce(new Error('boom'))

    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="email-load-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="email-switch"]').exists()).toBe(false)

    await wrapper.get('[data-testid="email-load-error"] button').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="email-switch"]').exists()).toBe(true)
  })
})

describe('push banner', () => {
  it('invites to turn push on while it is undecided', async () => {
    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="push-banner-enable"]').exists()).toBe(true)
  })

  it('turns push on from the banner and goes away', async () => {
    const wrapper = await mountBanner()

    await wrapper.get('[data-testid="push-banner-enable"]').trigger('click')
    await flushPromises()

    expect(pushApi.register).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(false)
  })

  it('goes away for good when dismissed', async () => {
    const wrapper = await mountBanner()

    await wrapper.get('[data-testid="push-banner-dismiss"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(false)
    expect(localStorage.getItem(PUSH_BANNER_DISMISSED_KEY)).toBe('1')

    mounted.splice(0).forEach(w => w.unmount())
    const again = await mountBanner()
    expect(again.find('[data-testid="push-banner"]').exists()).toBe(false)
  })

  it('stays hidden when push is on', async () => {
    browser.state.permission = 'granted'
    browser.state.subscription = subscription

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(false)
  })

  it('stays hidden when the browser blocks notifications', async () => {
    browser.state.permission = 'denied'

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(false)
  })

  it('stays hidden when push is not configured on the server', async () => {
    pushApi.vapidPublicKey.mockRejectedValue(Object.assign(new Error('503'), { response: { status: 503 } }))

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="push-banner"]').exists()).toBe(false)
  })

  it('shows the "add to home screen" hint instead of the switch where push is not supported', async () => {
    browser.state.supported = false

    const wrapper = await mountBanner()

    expect(wrapper.find('[data-testid="push-banner"]').text()).toContain('home screen')
    expect(wrapper.find('[data-testid="push-banner-enable"]').exists()).toBe(false)
  })

  it('reads the state of push only once per session', async () => {
    await mountBanner()
    expect(usePushStore().state).toBe('off')
    pushApi.vapidPublicKey.mockClear()

    await mountBanner()

    expect(pushApi.vapidPublicKey).not.toHaveBeenCalled()
  })
})
