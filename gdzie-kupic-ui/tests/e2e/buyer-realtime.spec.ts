import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'
import { postPath, sampleStatus, samplePost } from './support/posts'

// The buyer's request detail and home stay current through real-time events. The mock
// Playwright config runs with `realtimeMock`; the test plays the server through
// `window.__realtime` (see docs/api.md § Real-time events). Events only say "post X changed":
// the page always refetches status, responses and post over REST (mocked here, mutable).

type Json = Record<string, unknown>

interface Server {
  post: Json
  status: Json
  responses: Json[]
  statusCalls: number
  responseCalls: number
  postCalls: number
}

const response = (merchantId: string, shopName: string): Json => ({
  merchantId,
  shopName,
  state: 'HaveIt',
  threadId: `thread-${merchantId}`,
  unreadCount: 0,
  updatedAt: new Date().toISOString(),
})

function createServer(): Server {
  return {
    post: samplePost({ notificationDispatchStatus: 'Pending', notifiedCount: 0 }),
    status: sampleStatus({ notificationDispatchStatus: 'Pending', notifiedCount: 0 }),
    responses: [],
    statusCalls: 0,
    responseCalls: 0,
    postCalls: 0,
  }
}

async function openDetail(page: Page, server: Server) {
  await mockApi(page, [
    { method: 'GET', path: '/api/posts', respond: () => ({ json: [server.post] }) },
    { method: 'GET', path: postPath('/status'), respond: () => { server.statusCalls++; return { json: server.status } } },
    { method: 'GET', path: postPath('/responses'), respond: () => { server.responseCalls++; return { json: server.responses } } },
    { method: 'GET', path: postPath(), respond: () => { server.postCalls++; return { json: server.post } } },
  ])
  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')

  const isMobile = (page.viewportSize()?.width ?? 0) < 1024
  const nav = page.getByRole('navigation', { name: isMobile ? 'Nawigacja' : 'Nawigacja główna' })
  await nav.getByRole('link', { name: 'Zapytania' }).click()
  await page.getByRole('link', { name: /Rower górski/ }).click()
  await expect(page).toHaveURL(/\/requests\/p1$/)
  await expect(page.locator('h1', { hasText: 'Rower górski' })).toBeVisible()
}

const setState = (page: Page, state: string) => page.evaluate(s => window.__realtime!.setState(s), state)
const emit = (page: Page, name: string, payload?: unknown) => page.evaluate(([n, p]) => window.__realtime!.emit(n as string, p), [name, payload])

// Connects and waits until the resync has loaded and nothing is in flight any more, so that a test can count the requests.
async function connect(page: Page, server: Server) {
  await expect.poll(() => server.statusCalls).toBeGreaterThan(0) // the first load
  const before = server.statusCalls
  await setState(page, 'connected')
  await expect.poll(() => server.statusCalls).toBeGreaterThan(before) // the resync
  await page.waitForTimeout(500)
}

const rows = (page: Page) => page.getByTestId('response-row')
const row = (page: Page, key: string) => page.locator(`[data-testid="status-row"][data-key="${key}"]`)

// The "dispatch complete, a merchant answered" state of the server.
function merchantsResponded(server: Server) {
  server.status = sampleStatus({ notifiedCount: 5, haveItCount: 1, cannotHelpCount: 2 })
  server.responses = [response('a', 'Sklep Szybki')]
}

test.describe('Request detail — real-time', () => {
  test('a status change event refreshes the status panel and the responses without waiting for polling', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)
    await expect(page.getByTestId('notified-count')).toHaveText('0')
    await expect(rows(page)).toHaveCount(0)

    // Connected: the events are the only thing keeping the page current (no polling).
    await connect(page, server)
    merchantsResponded(server)

    await emit(page, 'postStatusChanged', { postId: 'p1' })

    await expect(page.getByTestId('notified-count')).toHaveText('5')
    await expect(row(page, 'have')).toContainText('1')
    await expect(rows(page)).toHaveCount(1)
    await expect(rows(page).first()).toContainText('Sklep Szybki')
  })

  test('ignores events of other requests and does not poll while connected', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)
    await connect(page, server)
    merchantsResponded(server)
    const calls = server.statusCalls

    await emit(page, 'postStatusChanged', { postId: 'another' })
    await page.waitForTimeout(6_500) // longer than the 5 s pending poll

    expect(server.statusCalls).toBe(calls)
    await expect(page.getByTestId('notified-count')).toHaveText('0')
  })

  test('a request that ends is reflected without a reload', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)
    await connect(page, server)
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toBeVisible()

    server.post = samplePost({ status: 'Expired' })
    await emit(page, 'postStatusChanged', { postId: 'p1' })

    await expect(page.getByTestId('post-status')).toHaveText('Wygasłe')
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toHaveCount(0)
  })

  test('a resync after reconnecting refetches what was missed', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)
    await connect(page, server)

    await setState(page, 'reconnecting')
    merchantsResponded(server)
    await setState(page, 'connected') // no event was received, only the resync

    await expect(page.getByTestId('notified-count')).toHaveText('5')
    await expect(rows(page)).toHaveCount(1)
  })

  test('polling takes over when the connection drops', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)
    await connect(page, server)
    merchantsResponded(server)

    await setState(page, 'reconnecting')

    await expect(page.getByTestId('notified-count')).toHaveText('5', { timeout: 15_000 })
    await expect(rows(page)).toHaveCount(1)
  })
})

test.describe('Buyer home — real-time', () => {
  test('a status change event updates the counts of the selected request', async ({ page }) => {
    const server = createServer()
    await mockApi(page, [
      { method: 'GET', path: '/api/posts', respond: () => ({ json: [server.post] }) },
      { method: 'GET', path: postPath('/status'), respond: () => { server.statusCalls++; return { json: server.status } } },
    ])
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await connect(page, server)

    merchantsResponded(server)
    await emit(page, 'postStatusChanged', { postId: 'p1' })

    await expect(row(page, 'have')).toContainText('1')
  })

  test('a request that ended leaves the list', async ({ page }) => {
    const server = createServer()
    await mockApi(page, [
      { method: 'GET', path: '/api/posts', respond: req => ({ json: req.url.searchParams.get('scope') === 'ended' ? [] : server.post.status === 'Active' ? [server.post] : [] }) },
      { method: 'GET', path: postPath('/status'), respond: () => ({ json: server.status }) },
    ])
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await setState(page, 'connected')
    await expect(page.getByText('Rower górski').first()).toBeVisible()

    server.post = samplePost({ status: 'Expired' })
    await emit(page, 'postStatusChanged', { postId: 'p1' })

    await expect(page.getByText('Rower górski')).toHaveCount(0)
  })
})
