import { readFileSync } from 'node:fs'
import { test, expect, request, type APIRequestContext } from '@playwright/test'
import { loginAs, openShellNav } from '../e2e/support/api-mock'

// Runs against the real backend (docker compose, API on NUXT_PUBLIC_API_BASE) with the seeded Dev
// accounts. Start it with `npm run test:e2e:real`.

const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5000'

// The mock accounts are the `token:` entries in `runtimeConfig.public.mockAccounts` (Admin, Buyer, Merchant).
function mockToken(index: 1 | 2): string {
  const config = readFileSync(new URL('../../nuxt.config.ts', import.meta.url), 'utf8')
  const tokens = [...config.matchAll(/token:\s*'([^']+)'/g)].map(m => m[1])
  return tokens[index]!
}

let buyer: APIRequestContext

test.beforeAll(async () => {
  buyer = await request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${mockToken(1)}` } })
})

test.afterAll(async () => {
  await buyer.dispose()
})

const emailEnabled = async () =>
  ((await (await buyer.get('/api/account/notification-settings')).json()) as { emailEnabled: boolean }).emailEnabled

test('a buyer saves the e-mail notification setting', async ({ page }) => {
  // The endpoint arrives with the service ticket "E-mail Settings & Buyer E-mail Notifications" (#138).
  const available = (await buyer.get('/api/account/notification-settings')).ok()
  test.fixme(!available, 'GET /api/account/notification-settings is not served yet (service ticket #138)')

  await buyer.put('/api/account/notification-settings', { data: { emailEnabled: false } })

  try {
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await openShellNav(page, 'Ustawienia')
    await expect(page).toHaveURL('/settings/notifications')

    const emailSwitch = page.getByRole('switch', { name: 'Powiadomienia e-mail' })
    await expect(emailSwitch).not.toBeChecked()

    await emailSwitch.click()

    await expect(emailSwitch).toBeChecked()
    expect(await emailEnabled()).toBe(true)

    await emailSwitch.click()

    await expect(emailSwitch).not.toBeChecked()
    expect(await emailEnabled()).toBe(false)
  }
  finally {
    await buyer.put('/api/account/notification-settings', { data: { emailEnabled: false } })
  }
})
