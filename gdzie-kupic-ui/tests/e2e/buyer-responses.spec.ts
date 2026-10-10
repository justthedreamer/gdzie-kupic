import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, type MockHandler } from './support/api-mock'
import { postPath, sampleStatus, samplePost } from './support/posts'

// The buyer's view of the answers: the real counts, the merchants that can help with a way
// into the chat, and the recent chats on the home page. Posts, status and responses are
// mocked here (API contract: planning/phase-5-merchant-response-chat.md); the chat itself
// runs on the app's `chatMock` sample threads (thread-feed-4 has 2 unread messages).

type Json = Record<string, unknown>

const response = (merchantId: string, overrides: Json = {}): Json => ({
  merchantId,
  shopName: `Sklep ${merchantId}`,
  state: 'HaveIt',
  threadId: `thread-${merchantId}`,
  unreadCount: 0,
  updatedAt: new Date().toISOString(),
  ...overrides,
})

interface Server {
  status: () => Json
  responses: () => Json[] | { status: number }
  statusCalls: number
  responseCalls: number
}

function createServer(overrides: Partial<Server> = {}): Server {
  const server: Server = {
    status: () => sampleStatus(),
    responses: () => [],
    statusCalls: 0,
    responseCalls: 0,
    ...overrides,
  }

  return server
}

function handlers(server: Server): MockHandler[] {
  return [
    { method: 'GET', path: '/api/posts', respond: () => ({ json: [samplePost()] }) },
    {
      method: 'GET',
      path: postPath('/status'),
      respond: () => {
        server.statusCalls++
        return { json: server.status() }
      },
    },
    {
      method: 'GET',
      path: postPath('/responses'),
      respond: () => {
        server.responseCalls++
        const result = server.responses()
        return Array.isArray(result) ? { json: result } : { status: result.status, json: { title: 'boom' } }
      },
    },
    { method: 'GET', path: postPath(), respond: () => ({ json: samplePost() }) },
  ]
}

async function openDetail(page: Page, server: Server) {
  await mockApi(page, handlers(server))
  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')

  const isMobile = (page.viewportSize()?.width ?? 0) < 1024
  const nav = page.getByRole('navigation', { name: isMobile ? 'Nawigacja' : 'Nawigacja główna' })
  await nav.getByRole('link', { name: 'Zapytania' }).click()
  await page.getByRole('link', { name: /Rower górski/ }).click()
  await expect(page).toHaveURL(/\/requests\/p1$/)
  await expect(page.locator('h1', { hasText: 'Rower górski' })).toBeVisible()
}

const rows = (page: Page) => page.getByTestId('response-row')

test.describe('Request detail — responses', () => {
  test('the status panel shows every kind of answer', async ({ page }) => {
    await openDetail(page, createServer({
      status: () => sampleStatus({
        notifiedCount: 12,
        checkingCount: 1,
        haveItCount: 2,
        mayHaveItCount: 1,
        canOrderItCount: 1,
        cannotHelpCount: 3,
      }),
    }))

    const count = (key: string) => page.locator(`[data-testid="status-row"][data-key="${key}"]`)
    await expect(page.getByTestId('notified-count')).toHaveText('12')
    await expect(count('checking')).toContainText('1')
    await expect(count('have')).toContainText('Ma produkt')
    await expect(count('have')).toContainText('2')
    await expect(count('may_have')).toContainText('Może mieć')
    await expect(count('may_have')).toContainText('1')
    await expect(count('can_order')).toContainText('Może zamówić')
    await expect(count('cannot')).toContainText('3')
    await expect(count('none')).toContainText('4')
  })

  test('lists the merchants that can help and opens the chat of the one that is clicked', async ({ page }) => {
    await openDetail(page, createServer({
      status: () => sampleStatus({ notifiedCount: 6, haveItCount: 1, mayHaveItCount: 1, cannotHelpCount: 4 }),
      responses: () => [
        response('feed-4', {
          shopName: 'Audio Shop Kraków',
          threadId: 'thread-feed-4',
          unreadCount: 2,
          updatedAt: new Date().toISOString(),
        }),
        response('b', { shopName: 'Muzyczny Raj', state: 'MayHaveIt', updatedAt: new Date(Date.now() - 3_600_000).toISOString() }),
      ],
    }))

    await expect(rows(page)).toHaveCount(2)
    // "Can't help" merchants are only in the counts.
    await expect(page.getByTestId('response-list')).not.toContainText('Nie pomoże')

    const first = rows(page).first()
    await expect(first).toContainText('Audio Shop Kraków')
    await expect(first.getByTestId('response-state')).toHaveText('Ma produkt')
    await expect(first.getByTestId('response-unread')).toHaveText('2')
    await expect(rows(page).nth(1).getByTestId('response-state')).toHaveText('Może mieć')
    await expect(rows(page).nth(1).getByTestId('response-unread')).toHaveCount(0)

    await first.getByRole('link').click()
    await expect(page).toHaveURL('/chat/thread-feed-4')
    await expect(page.getByTestId('chat-counterpart')).toHaveText('Audio Shop Kraków')
  })

  test('explains that nobody has confirmed yet', async ({ page }) => {
    await openDetail(page, createServer())

    await expect(page.getByTestId('responses-empty')).toContainText('Nikt jeszcze nie potwierdził')
    await expect(rows(page)).toHaveCount(0)
  })

  test('a failed load offers a retry', async ({ page }) => {
    let failing = true
    await openDetail(page, createServer({ responses: () => (failing ? { status: 500 } : [response('a')]) }))

    await expect(page.getByText('Nie udało się pobrać odpowiedzi sprzedawców.')).toBeVisible()

    failing = false
    await page.getByRole('button', { name: 'Spróbuj ponownie' }).click()

    await expect(rows(page)).toHaveCount(1)
  })

  test('the list refreshes with the status and stops when leaving the page', async ({ page }) => {
    const answers: Json[][] = [[], [response('late', { shopName: 'Sklep Spóźniony' })]]
    let answered = 0
    const server = createServer({
      // Pending matching polls every 5 s.
      status: () => sampleStatus({ notificationDispatchStatus: 'Pending', notifiedCount: 0 }),
      responses: () => answers[Math.min(answered++, answers.length - 1)]!,
    })
    await openDetail(page, server)

    await expect(page.getByTestId('responses-empty')).toBeVisible()
    await expect(rows(page)).toHaveCount(1, { timeout: 15_000 })
    await expect(rows(page).first()).toContainText('Sklep Spóźniony')

    await page.getByRole('link', { name: 'Wszystkie zapytania' }).click()
    await expect(page).toHaveURL('/requests')
    const after = server.responseCalls
    await page.waitForTimeout(6_500)

    expect(server.responseCalls).toBe(after)
  })
})

test.describe('Buyer home — status and recent chats', () => {
  test.beforeEach(async ({ page, isMobile }) => {
    test.skip(isMobile, 'recent chats are shown on desktop only')

    await mockApi(page, handlers(createServer({
      status: () => sampleStatus({ notifiedCount: 9, checkingCount: 1, haveItCount: 1, mayHaveItCount: 2, canOrderItCount: 1, cannotHelpCount: 2 }),
    })))
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
  })

  test('shows the split counts of the selected request', async ({ page }) => {
    await expect(page.locator('[data-testid="status-row"][data-key="may_have"]')).toContainText('2')
    await expect(page.locator('[data-testid="status-row"][data-key="can_order"]')).toContainText('1')
    await expect(page.locator('[data-testid="status-row"][data-key="none"]')).toContainText('2')
  })

  test('recent chats are the real threads with their unread counts and open the thread', async ({ page }) => {
    const threads = page.getByTestId('chat-thread-row')

    await expect(threads).toHaveCount(3)
    const audio = threads.filter({ hasText: 'Audio Shop Kraków' })
    await expect(audio.getByTestId('chat-thread-unread')).toHaveText('2')

    await audio.click()
    await expect(page).toHaveURL('/chat/thread-feed-4')
    await expect(page.getByTestId('chat-counterpart')).toHaveText('Audio Shop Kraków')
  })

  test('"all conversations" leads to the inbox', async ({ page }) => {
    await page.getByTestId('chats-view-all').click()

    await expect(page).toHaveURL('/chat')
  })
})
