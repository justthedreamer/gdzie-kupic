import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, openShellNav, type MockHandler } from './support/api-mock'

// Notification settings and the push banner. Push runs on the `pushMock` fake of the browser's
// push API (window.__pushInit / window.__push, see docs/api.md § Web Push); the service is mocked.

const device = {
  endpoint: 'https://push.example.test/send/device-1',
  keys: { p256dh: 'device-p256dh', auth: 'device-auth' },
}

const merchant = {
  merchantId: 'm1',
  name: 'Sklep Testowy',
  description: null,
  branch: { id: 'b1', displayName: 'Oddział Rynek', latitude: 50.06, longitude: 19.94, addressDisplayName: null, phone: null, website: null },
}

interface Server {
  registered: unknown[]
  removed: unknown[]
  emailPuts: unknown[]
}

interface Options {
  emailEnabled?: boolean
  /** `fail`: the request answers 500. */
  emailLoad?: 'fail'
  emailSave?: 'fail'
  vapid?: 'not-configured'
}

function createServer(options: Options = {}): { server: Server, handlers: MockHandler[] } {
  const server: Server = { registered: [], removed: [], emailPuts: [] }
  let emailEnabled = options.emailEnabled ?? false
  let loadsFailed = 0

  return {
    server,
    handlers: [
      { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: merchant }) },
      { method: 'GET', path: '/api/posts', respond: () => ({ json: [] }) },
      {
        method: 'GET',
        path: '/api/push/vapid-public-key',
        respond: () => options.vapid === 'not-configured'
          ? { status: 503, json: { title: 'Web Push is not configured' } }
          // A real VAPID public key: 65 bytes, URL-safe base64.
          : { json: { publicKey: 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM' } },
      },
      {
        method: 'PUT',
        path: '/api/push/subscription',
        respond: ({ body }) => {
          server.registered.push(body)
          return { status: 204 }
        },
      },
      {
        method: 'DELETE',
        path: '/api/push/subscription',
        respond: ({ body }) => {
          server.removed.push(body)
          return { status: 204 }
        },
      },
      {
        method: 'GET',
        path: '/api/account/notification-settings',
        // With `emailLoad: 'fail'` the first load fails (the client retries a failed GET once), so Retry can succeed.
        respond: () => options.emailLoad === 'fail' && loadsFailed++ < 2
          ? { status: 500, json: { title: 'boom' } }
          : { json: { emailEnabled } },
      },
      {
        method: 'PUT',
        path: '/api/account/notification-settings',
        respond: ({ body }) => {
          server.emailPuts.push(body)
          if (options.emailSave === 'fail') return { status: 500, json: { title: 'boom' } }
          emailEnabled = Boolean(body?.emailEnabled)
          return { json: { emailEnabled } }
        },
      },
    ],
  }
}

async function presetDevice(page: Page, init: Record<string, unknown>) {
  await page.addInitScript((value) => {
    (window as unknown as { __pushInit: unknown }).__pushInit = value
  }, init)
}

const NOT_DECIDED = { permission: 'default', subscription: null }

async function openSettings(page: Page, role: 'Buyer' | 'Merchant', handlers: MockHandler[]) {
  await mockApi(page, handlers)
  await loginAs(page, role)
  await expect(page).toHaveURL(role === 'Buyer' ? '/home' : '/feed')
  await openShellNav(page, role === 'Buyer' ? 'Ustawienia' : 'Powiadomienia')
  await expect(page).toHaveURL('/settings/notifications')
}

const pushSwitch = (page: Page) => page.getByRole('switch', { name: 'Powiadomienia na tym urządzeniu' })
const emailSwitch = (page: Page) => page.getByRole('switch', { name: 'Powiadomienia e-mail' })

test.describe('Notification settings - push', () => {
  test('a buyer turns push on and off for this device', async ({ page }) => {
    const { server, handlers } = createServer()
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', handlers)

    await expect(pushSwitch(page)).not.toBeChecked()

    await pushSwitch(page).click()

    await expect(pushSwitch(page)).toBeChecked()
    expect(server.registered).toHaveLength(1)
    expect(await page.evaluate(() => (window as unknown as { __push: { state: { prompts: number } } }).__push.state.prompts)).toBe(1)

    await pushSwitch(page).click()

    await expect(pushSwitch(page)).not.toBeChecked()
    expect(server.removed).toHaveLength(1)
  })

  test('a device that is already subscribed shows the switch on', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { permission: 'granted', subscription: device })
    await openSettings(page, 'Buyer', handlers)

    await expect(pushSwitch(page)).toBeChecked()
  })

  test('refusing the browser prompt explains how to unblock', async ({ page }) => {
    const { server, handlers } = createServer()
    await presetDevice(page, { ...NOT_DECIDED, promptResult: 'denied' })
    await openSettings(page, 'Buyer', handlers)

    await pushSwitch(page).click()

    await expect(pushSwitch(page)).toBeDisabled()
    await expect(page.getByTestId('push-hint')).toContainText('zablokowane w przeglądarce')
    expect(server.registered).toEqual([])
  })

  test('a browser that blocked notifications shows the explanation', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { permission: 'denied', subscription: null })
    await openSettings(page, 'Buyer', handlers)

    await expect(pushSwitch(page)).toBeDisabled()
    await expect(page.getByTestId('push-hint')).toContainText('zablokowane w przeglądarce')
  })

  test('a browser without push support shows the explanation', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { supported: false })
    await openSettings(page, 'Buyer', handlers)

    await expect(pushSwitch(page)).toBeDisabled()
    await expect(page.getByTestId('push-hint')).toContainText('ekranu głównego')
  })

  test('push that is not configured on the server disables the switch', async ({ page }) => {
    const { handlers } = createServer({ vapid: 'not-configured' })
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', handlers)

    await expect(pushSwitch(page)).toBeDisabled()
    await expect(page.getByTestId('push-hint')).toContainText('nie są teraz dostępne')
  })

  test('a failed registration shows an error and leaves the switch off', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', [
      { method: 'PUT', path: '/api/push/subscription', respond: () => ({ status: 500, json: { title: 'boom' } }) },
      ...handlers,
    ])

    await pushSwitch(page).click()

    await expect(page.getByTestId('push-error')).toBeVisible()
    await expect(pushSwitch(page)).not.toBeChecked()
  })
})

test.describe('Notification settings - e-mail', () => {
  test('a buyer saves the e-mail setting and sees buyer wording', async ({ page }) => {
    const { server, handlers } = createServer()
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', handlers)

    await expect(page.getByTestId('email-hint')).toContainText('odpowiedziach sprzedawców')
    await expect(emailSwitch(page)).not.toBeChecked()

    await emailSwitch(page).click()

    await expect(emailSwitch(page)).toBeChecked()
    expect(server.emailPuts).toEqual([{ emailEnabled: true }])
  })

  test('a merchant opens the page from "Powiadomienia" and sees merchant wording', async ({ page }) => {
    const { server, handlers } = createServer({ emailEnabled: true })
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Merchant', handlers)

    await expect(page.getByTestId('email-hint')).toContainText('podsumowanie nieprzetworzonych zapytań')
    await expect(emailSwitch(page)).toBeChecked()

    await emailSwitch(page).click()

    await expect(emailSwitch(page)).not.toBeChecked()
    expect(server.emailPuts).toEqual([{ emailEnabled: false }])
  })

  test('a failed save shows an error and keeps the previous state', async ({ page }) => {
    const { handlers } = createServer({ emailSave: 'fail' })
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', handlers)

    await emailSwitch(page).click()

    await expect(page.getByTestId('email-save-error')).toBeVisible()
    await expect(emailSwitch(page)).not.toBeChecked()
  })

  test('a failed load shows an error with a retry', async ({ page }) => {
    const { handlers } = createServer({ emailLoad: 'fail' })
    await presetDevice(page, NOT_DECIDED)
    await openSettings(page, 'Buyer', handlers)

    await expect(page.getByTestId('email-load-error')).toBeVisible()
    await expect(emailSwitch(page)).toHaveCount(0)

    await page.getByTestId('email-load-error').getByRole('button', { name: 'Spróbuj ponownie' }).click()

    await expect(emailSwitch(page)).toBeVisible()
  })

  test('an admin cannot open the page', async ({ page }) => {
    const { handlers } = createServer()
    await mockApi(page, handlers)
    await loginAs(page, 'Admin')
    await expect(page).toHaveURL('/admin/catalogue')

    // Nuxt exposes `useNuxtApp` on `window` in dev: a client-side navigation keeps the in-memory session.
    await page.evaluate(() => (window as unknown as { useNuxtApp: () => { $router: { push: (to: string) => Promise<unknown> } } }).useNuxtApp().$router.push('/settings/notifications'))

    await expect(page).not.toHaveURL(/\/settings\/notifications/)
    await expect(page.getByTestId('push-switch')).toHaveCount(0)
  })
})

test.describe('Push banner', () => {
  test('the buyer home invites to turn push on, and turning it on registers the device', async ({ page }) => {
    const { server, handlers } = createServer()
    await presetDevice(page, NOT_DECIDED)
    await mockApi(page, handlers)
    await loginAs(page, 'Buyer')

    await expect(page.getByTestId('push-banner')).toBeVisible()

    await page.getByTestId('push-banner-enable').click()

    await expect(page.getByTestId('push-banner')).toHaveCount(0)
    expect(server.registered).toHaveLength(1)
  })

  test('the merchant feed shows the banner and dismissing it is remembered on this device', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, NOT_DECIDED)
    await mockApi(page, handlers)
    await loginAs(page, 'Merchant')
    await expect(page.getByTestId('push-banner')).toBeVisible()

    await page.getByTestId('push-banner-dismiss').click()

    await expect(page.getByTestId('push-banner')).toHaveCount(0)
    expect(await page.evaluate(() => localStorage.getItem('gk:push-banner-dismissed'))).toBe('1')

    // Away and back (client-side: a reload would end the in-memory session).
    await openShellNav(page, 'Powiadomienia')
    await expect(page).toHaveURL('/settings/notifications')
    await page.goBack()
    await expect(page).toHaveURL('/feed')
    await expect(page.getByTestId('push-banner')).toHaveCount(0)
  })

  test('is not shown when push is already on', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { permission: 'granted', subscription: device })
    await mockApi(page, handlers)
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await page.waitForTimeout(500)

    await expect(page.getByTestId('push-banner')).toHaveCount(0)
  })

  test('is not shown when the browser blocks notifications', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { permission: 'denied', subscription: null })
    await mockApi(page, handlers)
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await page.waitForTimeout(500)

    await expect(page.getByTestId('push-banner')).toHaveCount(0)
  })

  test('is not shown when push is not configured on the server', async ({ page }) => {
    const { handlers } = createServer({ vapid: 'not-configured' })
    await presetDevice(page, NOT_DECIDED)
    await mockApi(page, handlers)
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await page.waitForTimeout(500)

    await expect(page.getByTestId('push-banner')).toHaveCount(0)
  })

  test('shows the "add to home screen" hint where push is not supported', async ({ page }) => {
    const { handlers } = createServer()
    await presetDevice(page, { supported: false })
    await mockApi(page, handlers)
    await loginAs(page, 'Buyer')

    await expect(page.getByTestId('push-banner')).toContainText('ekranu głównego')
    await expect(page.getByTestId('push-banner-enable')).toHaveCount(0)
  })
})
