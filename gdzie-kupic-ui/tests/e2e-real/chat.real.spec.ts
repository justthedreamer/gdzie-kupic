import { readFileSync } from 'node:fs'
import { test, expect, request, type APIRequestContext, type Page } from '@playwright/test'
import { loginAs } from '../e2e/support/api-mock'

// Runs against the real backend (docker compose, API on NUXT_PUBLIC_API_BASE) with the
// seeded Dev accounts. Start it with `npm run test:e2e:real`. The merchant account must be
// onboarded and subscribed to at least one tag (the seed does that), otherwise there is
// nobody to chat with.

const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5000'

// The mock accounts are the `token:` entries in `runtimeConfig.public.mockAccounts` (Admin, Buyer, Merchant).
function mockToken(index: 1 | 2): string {
  const config = readFileSync(new URL('../../nuxt.config.ts', import.meta.url), 'utf8')
  const tokens = [...config.matchAll(/token:\s*'([^']+)'/g)].map(m => m[1])
  return tokens[index]!
}

interface Category { id: string, name: string, isDisabled: boolean, tags: { id: string, name: string, isDisabled: boolean }[] }
interface Subscription { categoryId: string, tagId: string | null }
interface MerchantProfile { name: string, branch: { latitude: number, longitude: number } }
interface Message { body: string | null, isMine: boolean }

let buyer: APIRequestContext
let merchant: APIRequestContext

test.beforeAll(async () => {
  buyer = await request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${mockToken(1)}` } })
  merchant = await request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${mockToken(2)}` } })
})

test.afterAll(async () => {
  await buyer.dispose()
  await merchant.dispose()
})

/** A request near the merchant's branch in a category the merchant listens to, and the merchant's positive response. */
async function openThreadWithMerchant(title: string) {
  const profile = await (await merchant.get('/api/merchant/me')).json() as MerchantProfile
  const subscriptions = await (await merchant.get('/api/merchant/subscriptions')).json() as Subscription[]
  const categories = await (await buyer.get('/api/catalogue/categories')).json() as Category[]

  const target = categories
    .filter(c => !c.isDisabled)
    .flatMap(c => c.tags.filter(t => !t.isDisabled).map(t => ({ categoryId: c.id, tagId: t.id })))
    .find(x => subscriptions.some(s => s.categoryId === x.categoryId && (s.tagId === null || s.tagId === x.tagId)))
  expect(target, 'the merchant needs a subscription to an enabled tag').toBeTruthy()

  const created = await buyer.post('/api/posts', {
    data: {
      latitude: profile.branch.latitude,
      longitude: profile.branch.longitude,
      radiusKm: 10,
      categoryId: target!.categoryId,
      tagId: target!.tagId,
      title,
      urgentDeadline: new Date(Date.now() + 2 * 3600_000).toISOString(),
    },
  })
  expect(created.status()).toBe(201)
  const post = await created.json() as { id: string }

  // Matching runs in a background job: the post reaches the merchant's feed after a while.
  await expect.poll(async () => {
    const feed = await (await merchant.get('/api/merchant/feed')).json() as { items: { id: string }[] }
    return feed.items.some(item => item.id === post.id)
  }, { timeout: 60_000, intervals: [1_000] }).toBe(true)

  const response = await merchant.put(`/api/merchant/feed/${post.id}/response`, { data: { state: 'HaveIt' } })
  expect(response.ok()).toBe(true)
  const { threadId } = await response.json() as { threadId: string }
  expect(threadId).toBeTruthy()

  return { threadId, shopName: profile.name }
}

async function messagesOf(api: APIRequestContext, threadId: string): Promise<Message[]> {
  const page = await (await api.get(`/api/chat/threads/${threadId}/messages`)).json() as { items: Message[] }
  return page.items
}

// dispatchEvent: on mobile the Nuxt devtools overlay covers the bottom tab bar links.
async function openChats(page: Page) {
  await page.getByRole('link', { name: /Czaty/ }).first().dispatchEvent('click')
  await expect(page).toHaveURL('/chat')
}

test('the buyer and the merchant talk in the thread a positive response opened', async ({ page }) => {
  test.setTimeout(120_000)
  const title = `E2E czat ${Date.now()}`
  const { threadId, shopName } = await openThreadWithMerchant(title)

  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')
  await openChats(page)

  // The new thread is in the inbox with the shop and the request.
  const row = page.getByTestId('chat-thread-row').filter({ hasText: title })
  await expect(row).toContainText(shopName)
  await row.click()
  await expect(page).toHaveURL(`/chat/${threadId}`)
  await expect(page.getByTestId('chat-counterpart')).toHaveText(shopName)
  await expect(page.getByTestId('chat-request-link')).toContainText(title)

  // The buyer writes; the merchant sees it on the server.
  const question = `Czy jest dostępne? ${Date.now()}`
  await page.getByRole('textbox', { name: 'Wiadomość' }).fill(question)
  await page.getByRole('textbox', { name: 'Wiadomość' }).press('Enter')
  const own = page.getByTestId('chat-message').filter({ hasText: question })
  await expect(own).toHaveAttribute('data-state', 'sent')
  await expect(own).toHaveAttribute('data-own', 'true')
  await expect.poll(async () => (await messagesOf(merchant, threadId)).some(m => m.body === question && !m.isMine)).toBe(true)

  // The merchant answers; the open thread picks the answer up by polling.
  const answer = `Tak, zapraszamy. ${Date.now()}`
  const sent = await merchant.post(`/api/chat/threads/${threadId}/messages`, { multipart: { body: answer } })
  expect(sent.status()).toBe(201)
  const incoming = page.getByTestId('chat-message').filter({ hasText: answer })
  await expect(incoming).toBeVisible({ timeout: 15_000 })
  await expect(incoming).toHaveAttribute('data-own', 'false')

  // Reading it left nothing unread: the inbox shows the thread without a badge.
  await page.getByTestId('chat-back').click()
  await expect(row).toContainText(answer)
  await expect(row.getByTestId('chat-thread-unread')).toHaveCount(0)
})

test('a message the merchant sends while the buyer is on the inbox shows up as unread', async ({ page }) => {
  test.setTimeout(120_000)
  const title = `E2E czat nieprzeczytane ${Date.now()}`
  const { threadId } = await openThreadWithMerchant(title)

  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')
  await openChats(page)

  const row = page.getByTestId('chat-thread-row').filter({ hasText: title })
  await expect(row).toBeVisible()
  await expect(row.getByTestId('chat-thread-unread')).toHaveCount(0)

  const sent = await merchant.post(`/api/chat/threads/${threadId}/messages`, { multipart: { body: 'Dzień dobry, mamy to w sklepie.' } })
  expect(sent.status()).toBe(201)

  // The inbox refreshes in the background (and the navigation badge with it).
  await expect(row.getByTestId('chat-thread-unread')).toHaveText('1', { timeout: 45_000 })
  await expect(row).toContainText('Dzień dobry, mamy to w sklepie.')
})
