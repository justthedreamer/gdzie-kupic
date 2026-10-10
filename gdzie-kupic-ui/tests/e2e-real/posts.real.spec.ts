import { readFileSync } from 'node:fs'
import { test, expect, request, type APIRequestContext } from '@playwright/test'
import { loginAs } from '../e2e/support/api-mock'

// Runs against the real backend (docker compose, API on NUXT_PUBLIC_API_BASE) with the
// seeded Dev accounts. Start it with `npm run test:e2e:real`. The service must use the
// JWT secret the mock tokens were signed with (see docs/local-dev.md § Mock Accounts).

const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5000'

// The mock accounts are the `token:` entries in `runtimeConfig.public.mockAccounts` (Admin, Buyer, Merchant).
function mockToken(index: 1 | 2): string {
  const config = readFileSync(new URL('../../nuxt.config.ts', import.meta.url), 'utf8')
  const tokens = [...config.matchAll(/token:\s*'([^']+)'/g)].map(m => m[1])
  return tokens[index]!
}

interface Category { id: string, name: string, isDisabled: boolean, tags: { id: string, name: string, isDisabled: boolean }[] }

let api: APIRequestContext
let categoryName: string
let tagName: string
let locationLabel: string
let subscribed: { categoryId: string, tagId: string } | undefined

test.beforeAll(async () => {
  api = await request.newContext({
    baseURL: API_BASE,
    extraHTTPHeaders: { Authorization: `Bearer ${mockToken(1)}` },
  })

  // Zero-match needs a tag no merchant is subscribed to (directly or through its category).
  const merchant = await request.newContext({
    baseURL: API_BASE,
    extraHTTPHeaders: { Authorization: `Bearer ${mockToken(2)}` },
  })
  const subscriptionsResponse = await merchant.get('/api/merchant/subscriptions')
  const subscriptions = subscriptionsResponse.ok()
    ? await subscriptionsResponse.json() as { categoryId: string, tagId: string | null }[]
    : []
  await merchant.dispose()

  const categories = await (await api.get('/api/catalogue/categories')).json() as Category[]
  const enabled = categories.filter(c => !c.isDisabled)
  subscribed = enabled
    .flatMap(c => c.tags.filter(t => !t.isDisabled).map(t => ({ categoryId: c.id, tagId: t.id })))
    .find(x => subscriptions.some(s => s.categoryId === x.categoryId && (s.tagId === null || s.tagId === x.tagId)))
  const unsubscribed = categories
    .filter(c => !c.isDisabled && !subscriptions.some(s => s.categoryId === c.id && s.tagId === null))
    .flatMap(c => c.tags.filter(t => !t.isDisabled && !subscriptions.some(s => s.tagId === t.id)).map(t => ({ c, t })))[0]
  expect(unsubscribed, 'the catalogue needs an enabled tag without merchant subscriptions').toBeTruthy()
  categoryName = unsubscribed!.c.name
  tagName = unsubscribed!.t.name

  const saved = await (await api.get('/api/saved-locations')).json() as { displayName: string }[]
  if (saved.length === 0) {
    const res = await api.post('/api/saved-locations', {
      data: { displayName: 'Dom (e2e)', latitude: 52.2297, longitude: 21.0122 },
    })
    expect(res.status()).toBe(201)
    locationLabel = 'Dom (e2e)'
  }
  else {
    locationLabel = saved[0]!.displayName
  }
})

test.afterAll(async () => {
  await api.dispose()
})

async function choose(page: import('@playwright/test').Page, label: string, option: string) {
  await page.getByRole('combobox', { name: label }).click()
  await page.getByRole('option', { name: option }).click()
}

test('a request goes from the form through zero-match extension to "found"', async ({ page }) => {
  // Matching runs in a background job, so the zero-match result takes a while.
  test.setTimeout(120_000)
  const title = `E2E rower ${Date.now()}`

  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')

  // dispatchEvent: on mobile the Nuxt devtools overlay covers the bottom tab bar link.
  await page.getByRole('link', { name: 'Nowe zapytanie' }).first().dispatchEvent('click')
  await expect(page).toHaveURL('/requests/new')

  await page.getByLabel('Tytuł').fill(title)
  await choose(page, 'Kategoria', categoryName)
  await choose(page, 'Tag', tagName)
  await page.getByRole('radio', { name: new RegExp(locationLabel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')) }).click()
  await page.getByRole('radio', { name: '10 km' }).click()
  await page.getByRole('button', { name: 'Opublikuj zapytanie' }).click()

  // Created: the detail page of the real post.
  await expect(page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/)
  await expect(page.locator('h1', { hasText: title })).toBeVisible()
  await expect(page.getByTestId('post-status')).toHaveText('Aktywne')

  // Matching runs in a background job: the post is "Pending" first, then dispatched. With no
  // merchant nearby the result is zero-match, which offers extending the request.
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByText('Nikogo jeszcze nie znaleźliśmy')).toBeVisible({ timeout: 60_000 })
  const before = await page.getByTestId('post-expires').innerText()
  await dialog.getByRole('button', { name: 'Przedłuż do 14 dni' }).click()

  await expect(dialog).toBeHidden()
  await expect(page.getByText('Długo aktywne')).toBeVisible()
  await expect(page.getByTestId('post-expires')).not.toHaveText(before)

  // Finish it and find it on the Ended tab.
  await page.getByRole('button', { name: 'Znalazłem' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Tak, znalazłem' }).click()
  await expect(page.getByTestId('post-status')).toHaveText('Znalezione')

  await page.getByRole('link', { name: 'Wszystkie zapytania' }).click()
  await expect(page).toHaveURL('/requests')
  await expect(page.getByTestId('request-card').filter({ hasText: title })).toHaveCount(0)
  await page.getByRole('tab', { name: 'Zakończone' }).click()
  await expect(page.getByTestId('request-card').filter({ hasText: title })).toBeVisible()
})

test('a closed request cannot be ended again (409 is explained)', async ({ page }) => {
  const title = `E2E zamykanie ${Date.now()}`
  const categories = await (await api.get('/api/catalogue/categories')).json() as Category[]
  const category = categories.find(c => c.name === categoryName)!
  const tag = category.tags.find(t => t.name === tagName)!

  const created = await api.post('/api/posts', {
    // Urgent: no zero-match popup gets in the way of the buttons.
    data: {
      latitude: 52.2297,
      longitude: 21.0122,
      radiusKm: 10,
      categoryId: category.id,
      tagId: tag.id,
      title,
      urgentDeadline: new Date(Date.now() + 2 * 3600_000).toISOString(),
    },
  })
  expect(created.status()).toBe(201)
  const post = await created.json() as { id: string }

  await loginAs(page, 'Buyer')
  await page.getByRole('link', { name: 'Zapytania' }).first().dispatchEvent('click')
  await page.getByRole('link', { name: new RegExp(title) }).click()
  await expect(page.locator('h1', { hasText: title })).toBeVisible()

  // The same request is ended elsewhere (another tab / device) before the buyer acts.
  expect((await api.post(`/api/posts/${post.id}/close`)).status()).toBe(204)

  await page.getByRole('button', { name: 'Zamknij zapytanie' }).first().click()
  await page.getByRole('dialog').getByRole('button', { name: 'Zamknij zapytanie' }).click()

  await expect(page.getByTestId('request-notice')).toContainText('To zapytanie nie jest już aktywne')
  await expect(page.getByTestId('post-status')).toHaveText('Zamknięte')
})

test('a request in a subscribed tag notifies the merchant and skips the zero-match popup', async ({ page }) => {
  test.skip(!subscribed, 'the Dev Merchant has no subscriptions')
  test.setTimeout(120_000)
  const title = `E2E dopasowanie ${Date.now()}`

  // Unlimited radius: the match does not depend on where the Merchant's branch is.
  const created = await api.post('/api/posts', {
    data: { latitude: 52.2297, longitude: 21.0122, radiusKm: null, ...subscribed, title },
  })
  expect(created.status()).toBe(201)

  await loginAs(page, 'Buyer')
  await page.getByRole('link', { name: 'Zapytania' }).first().dispatchEvent('click')
  await page.getByRole('link', { name: new RegExp(title) }).click()
  await expect(page.locator('h1', { hasText: title })).toBeVisible()

  await expect(page.getByTestId('notified-count')).toHaveText('1', { timeout: 60_000 })
  await expect(page.getByText('Szukamy sprzedawców…')).toHaveCount(0)
  await expect(page.getByRole('dialog')).toHaveCount(0)
})
