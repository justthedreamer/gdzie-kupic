import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, type MockHandler } from './support/api-mock'

// The mock Playwright config runs with `pushMock`: the browser's push API is a fake that the
// test controls through `window.__pushInit` (before the app starts) and `window.__push`
// (see docs/api.md § Web Push). Registration at the service is mocked here (contract:
// planning/phase-7-push-notifications.md).

const device = {
  endpoint: 'https://push.example.test/send/device-1',
  keys: { p256dh: 'device-p256dh', auth: 'device-auth' },
}

interface Server {
  registered: unknown[]
  removed: unknown[]
}

function createServer(): { server: Server, handlers: MockHandler[] } {
  const server: Server = { registered: [], removed: [] }

  return {
    server,
    handlers: [
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
      // The Merchant guard asks for the merchant profile before opening the feed.
      {
        method: 'GET',
        path: '/api/merchant/me',
        respond: () => ({
          json: {
            merchantId: 'm1',
            name: 'Sklep Testowy',
            description: null,
            branch: { id: 'b1', displayName: 'Oddział Rynek', latitude: 50.06, longitude: 19.94, addressDisplayName: null, phone: null, website: null },
          },
        }),
      },
    ],
  }
}

/** The device state before the app starts. */
async function presetDevice(page: Page, init: Record<string, unknown>) {
  await page.addInitScript((value) => {
    (window as unknown as { __pushInit: unknown }).__pushInit = value
  }, init)
}

async function signOut(page: Page, isMobile: boolean) {
  // Logout lives in the sidebar on desktop and in the header menu on mobile.
  if (isMobile) {
    await page.getByRole('button', { name: 'Menu' }).click()
    await page.getByRole('menuitem', { name: 'Wyloguj się' }).click()
  }
  else {
    await page.getByRole('button', { name: 'Wyloguj się' }).click()
  }
}

test.describe('Push registration', () => {
  test('a device with push on is registered for the user after sign-in', async ({ page }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'granted', subscription: device })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')

    await expect.poll(() => server.registered).toEqual([device])
  })

  test('a device that has not decided about push is not registered', async ({ page }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'default', subscription: null })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    // Registration would follow sign-in at once.
    await page.waitForTimeout(500)

    expect(server.registered).toEqual([])
  })

  test('a device where push is blocked is not registered', async ({ page }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'denied', subscription: null })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await page.waitForTimeout(500)

    expect(server.registered).toEqual([])
  })

  test('switching to another account registers the device for that account too', async ({ page }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'granted', subscription: device })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await expect.poll(() => server.registered.length).toBe(1)

    await page.getByRole('button', { name: 'Dev', exact: true }).click()
    await page.getByRole('menuitem', { name: /^Merchant/ }).click()
    await expect(page).toHaveURL('/feed')

    await expect.poll(() => server.registered).toEqual([device, device])
  })

  test('signing out removes the registration of the device', async ({ page, isMobile }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'granted', subscription: device })

    await loginAs(page, 'Buyer')
    await expect.poll(() => server.registered.length).toBe(1)

    await signOut(page, isMobile)

    await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
    expect(server.removed).toEqual([{ endpoint: device.endpoint }])
    // The browser keeps its subscription: the next sign-in registers it again.
    expect(await page.evaluate(() => (window as unknown as { __push: { state: { subscription: unknown } } }).__push.state.subscription)).toEqual(device)
  })

  test('signing out of a device without push sends nothing', async ({ page, isMobile }) => {
    const { server, handlers } = createServer()
    await mockApi(page, handlers)
    await presetDevice(page, { permission: 'default', subscription: null })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await signOut(page, isMobile)

    await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
    expect(server.removed).toEqual([])
  })

  test('signing out still works when the service fails', async ({ page, isMobile }) => {
    const { handlers } = createServer()
    await mockApi(page, [
      { method: 'DELETE', path: '/api/push/subscription', respond: () => ({ status: 500, json: { title: 'boom' } }) },
      ...handlers.filter(handler => handler.method !== 'DELETE'),
    ])
    await presetDevice(page, { permission: 'granted', subscription: device })

    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await signOut(page, isMobile)

    await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
  })
})
