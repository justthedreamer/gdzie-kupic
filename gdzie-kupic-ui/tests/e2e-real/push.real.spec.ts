import { readFileSync } from 'node:fs'
import { test, expect, request, type APIRequestContext } from '@playwright/test'

// Runs against the real backend (docker compose, API on NUXT_PUBLIC_API_BASE) with the seeded Dev
// accounts. Start it with `npm run test:e2e:real`. The service needs a VAPID key pair
// (VAPID_PUBLIC_KEY / VAPID_PRIVATE_KEY in .env, see docs/local-dev.md); without it the specs skip.
// Only the service contract is checked here: headless Chromium cannot create a real push
// subscription (no push service, and `Notification.permission` stays "denied"), so turning push on
// in the browser is covered by the mock specs (tests/e2e/push.spec.ts, notification-settings.spec.ts).

const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5000'

// The mock accounts are the `token:` entries in `runtimeConfig.public.mockAccounts` (Admin, Buyer, Merchant).
function mockToken(index: 0 | 1 | 2): string {
  const config = readFileSync(new URL('../../nuxt.config.ts', import.meta.url), 'utf8')
  const tokens = [...config.matchAll(/token:\s*'([^']+)'/g)].map(m => m[1])
  return tokens[index]!
}

let buyer: APIRequestContext
let admin: APIRequestContext
let vapidKey: string | null = null

test.beforeAll(async () => {
  buyer = await request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${mockToken(1)}` } })
  admin = await request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${mockToken(0)}` } })
  const response = await buyer.get('/api/push/vapid-public-key')
  if (response.ok()) vapidKey = ((await response.json()) as { publicKey: string }).publicKey
})

test.afterAll(async () => {
  await buyer.dispose()
  await admin.dispose()
})

const subscription = (endpoint: string) => ({ endpoint, keys: { p256dh: 'BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM', auth: 'tBHItJI5svbpez7KI4CCXg' } })

test.describe('push endpoints', () => {
  test.skip(() => vapidKey === null, 'Web Push is not configured on the service (VAPID keys missing)')

  test('the service hands out the VAPID public key as { publicKey }', () => {
    expect(vapidKey).toMatch(/^[A-Za-z0-9_-]{80,}$/)
  })

  test('a device is registered and removed again, both idempotent', async () => {
    const body = subscription(`https://push.example.test/real-e2e/${Date.now()}`)

    expect((await buyer.put('/api/push/subscription', { data: body })).status()).toBe(204)
    expect((await buyer.put('/api/push/subscription', { data: body })).status()).toBe(204)
    expect((await buyer.delete('/api/push/subscription', { data: { endpoint: body.endpoint } })).status()).toBe(204)
    expect((await buyer.delete('/api/push/subscription', { data: { endpoint: body.endpoint } })).status()).toBe(204)
  })

  test('a registration that is not a valid push endpoint is rejected', async () => {
    const response = await buyer.put('/api/push/subscription', { data: subscription('not-a-url') })

    expect(response.status()).toBe(400)
  })

  test('an admin has no push', async () => {
    expect((await admin.get('/api/push/vapid-public-key')).status()).toBe(403)
  })
})
