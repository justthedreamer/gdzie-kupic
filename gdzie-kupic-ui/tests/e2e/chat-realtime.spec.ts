import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'
import { postPath, sampleStatus, samplePost } from './support/posts'

// Chat, unread badges and notification toasts follow the server's events. The mock Playwright
// config runs with `realtimeMock` (the test plays the server through `window.__realtime`) and
// `chatMock` (app/mocks/chat.ts). The sessionStorage hooks of the chat mock play the other
// side: `gk:mock-chat-incoming` queues the counterpart's messages, `gk:mock-chat-lock` locks
// threads. Events only name a thread; the app always refetches over REST.
//
// A buyer starts with: thread-feed-4 (Audio Shop Kraków, 2 unread), thread-closed (Studio
// Sklep, 0), thread-locked (Muzyczny Raj, 0). The unread badge on "Czaty" shows 2.

const queue = (page: Page, threadId: string, body: string) =>
  page.evaluate(([id, text]) => sessionStorage.setItem('gk:mock-chat-incoming', JSON.stringify([{ threadId: id, body: text }])), [threadId, body])
const lock = (page: Page, threadId: string) =>
  page.evaluate(id => sessionStorage.setItem('gk:mock-chat-lock', JSON.stringify([id])), threadId)
const emit = (page: Page, name: string, payload?: unknown) =>
  page.evaluate(([n, p]) => window.__realtime!.emit(n as string, p), [name, payload])
const setState = (page: Page, state: string) => page.evaluate(s => window.__realtime!.setState(s), state)

// Connects and waits until the resync has settled, so that nothing is in flight any more.
async function connect(page: Page) {
  await setState(page, 'connected')
  await page.waitForTimeout(500)
}

async function hideDevtools(page: Page) {
  await page.addStyleTag({ content: '#nuxt-devtools-container { display: none !important; }' })
}

const badge = (page: Page) => page.getByRole('link', { name: /Czaty/ }).getByTestId('nav-badge')
const rows = (page: Page) => page.getByTestId('chat-thread-row')
const rowOf = (page: Page, counterpart: string) => rows(page).filter({ hasText: counterpart })
const messages = (page: Page) => page.getByTestId('chat-message')

async function openChats(page: Page) {
  await page.getByRole('link', { name: /Czaty/ }).click()
  await expect(page).toHaveURL('/chat')
}

async function openThread(page: Page, counterpart: string) {
  await rowOf(page, counterpart).click()
  await expect(page.getByTestId('chat-counterpart')).toHaveText(counterpart)
}

const NEW_MESSAGE = 'Masz nową wiadomość'
const RESPONDED = 'Sprzedawca odpowiedział na Twoje zapytanie'

// The buyer's home page needs a request to show its panels, among them the inbox.
const postsApi = [
  { method: 'GET' as const, path: '/api/posts', respond: () => ({ json: [samplePost()] }) },
  { method: 'GET' as const, path: postPath('/status'), respond: () => ({ json: sampleStatus() }) },
  { method: 'GET' as const, path: postPath('/responses'), respond: () => ({ json: [] }) },
  { method: 'GET' as const, path: postPath(), respond: () => ({ json: samplePost() }) },
]

test.describe('Chat — real-time', () => {
  test.beforeEach(async ({ page }) => {
    await mockApi(page, postsApi)
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await hideDevtools(page)
    await expect(badge(page)).toHaveText('2')
  })

  test('a message for another thread updates the badge and the inbox on the home page', async ({ page }) => {
    await expect(rows(page)).toHaveCount(3) // the inbox of the home page (shown on wide screens only)
    await connect(page)

    await queue(page, 'thread-closed', 'Wysyłamy kurierem jeszcze dziś.')
    await emit(page, 'messageReceived', { threadId: 'thread-closed', messageId: 'x1' })

    await expect(badge(page)).toHaveText('3')
    const row = rowOf(page, 'Studio Sklep')
    await expect(row).toContainText('Wysyłamy kurierem jeszcze dziś.')
    await expect(row.getByTestId('chat-thread-unread')).toHaveText('1')
  })

  test('a message in the open thread appears at once and is marked read', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await expect(badge(page)).toHaveText('2')
    await connect(page)

    await queue(page, 'thread-closed', 'Wysyłamy kurierem jeszcze dziś.')
    await emit(page, 'messageReceived', { threadId: 'thread-closed', messageId: 'x1' })

    await expect(messages(page).last()).toContainText('Wysyłamy kurierem jeszcze dziś.')
    await expect(messages(page).last()).toHaveAttribute('data-own', 'false')
    await expect(badge(page)).toHaveText('2') // it was read on arrival, the badge never counted it
    await page.getByTestId('chat-back').click()
    await expect(rowOf(page, 'Studio Sklep').getByTestId('chat-thread-unread')).toHaveCount(0)
  })

  test('a thread created meanwhile appears in the inbox', async ({ page }) => {
    await openChats(page)
    await expect(rows(page)).toHaveCount(3)
    await connect(page)

    await queue(page, 'thread-feed-2', 'Dzień dobry, mamy ten interfejs.')
    await emit(page, 'threadUpdated', { threadId: 'thread-feed-2' })

    await expect(rows(page)).toHaveCount(4)
    await expect(badge(page)).toHaveText('3')
  })

  test('a thread that got locked shows it in the inbox', async ({ page }) => {
    await openChats(page)
    await connect(page)
    await expect(rowOf(page, 'Studio Sklep')).not.toContainText('Zablokowana')

    await lock(page, 'thread-closed')
    await emit(page, 'threadUpdated', { threadId: 'thread-closed' })

    await expect(rowOf(page, 'Studio Sklep')).toContainText('Zablokowana')
  })

  test('the open thread learns that it was locked', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await connect(page)
    await expect(page.getByTestId('chat-locked')).toHaveCount(0)

    await lock(page, 'thread-closed')
    await emit(page, 'threadUpdated', { threadId: 'thread-closed' })

    await expect(page.getByTestId('chat-locked')).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'Wiadomość' })).toHaveCount(0) // the composer gives way to the notice
  })

  test('a new message notification shows a toast that opens the conversation', async ({ page }) => {
    await connect(page)

    await emit(page, 'notificationRaised', { kind: 'newMessage', postId: 'feed-4', threadId: 'thread-closed' })

    await expect(page.getByText(NEW_MESSAGE).first()).toBeVisible()
    await page.getByRole('button', { name: 'Otwórz rozmowę' }).click()
    await expect(page).toHaveURL('/chat/thread-closed')
  })

  test('a merchant response notification shows a toast that opens the request', async ({ page }) => {
    await connect(page)

    await emit(page, 'notificationRaised', { kind: 'merchantResponded', postId: 'p1', threadId: null })

    await expect(page.getByText(RESPONDED).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Zobacz zapytanie' })).toBeVisible()
  })

  test('no toast while the user is in that thread, but one for another thread', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await connect(page)

    await emit(page, 'notificationRaised', { kind: 'newMessage', postId: null, threadId: 'thread-closed' })
    await emit(page, 'notificationRaised', { kind: 'merchantResponded', postId: 'p9', threadId: null })

    await expect(page.getByText(RESPONDED).first()).toBeVisible() // the later one arrived...
    await expect(page.getByText(NEW_MESSAGE)).toHaveCount(0) // ...the one for this thread did not
  })

  test('a resync after a reconnect refetches the inbox and the badge', async ({ page }) => {
    await queue(page, 'thread-closed', 'Wysyłamy kurierem jeszcze dziś.')

    await setState(page, 'connected') // no event, only the resync

    await expect(badge(page)).toHaveText('3')
    await expect(rowOf(page, 'Studio Sklep')).toContainText('Wysyłamy kurierem jeszcze dziś.')
  })

  test('the open thread is polled only while the connection is down', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await connect(page)
    const before = await messages(page).count()

    await queue(page, 'thread-closed', 'Wysyłamy kurierem jeszcze dziś.')
    await page.waitForTimeout(6000) // more than one polling interval
    await expect(messages(page)).toHaveCount(before) // connected: no event, no poll

    await setState(page, 'reconnecting')
    await expect(messages(page).last()).toContainText('Wysyłamy kurierem jeszcze dziś.', { timeout: 10_000 })
  })
})

test.describe('Chat — real-time, on the request page', () => {
  test('no toast about a response while the buyer is looking at that request', async ({ page }) => {
    await mockApi(page, postsApi)
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await hideDevtools(page)

    const isMobile = (page.viewportSize()?.width ?? 0) < 1024
    const nav = page.getByRole('navigation', { name: isMobile ? 'Nawigacja' : 'Nawigacja główna' })
    await nav.getByRole('link', { name: 'Zapytania' }).click()
    await page.getByRole('link', { name: /Rower górski/ }).click()
    await expect(page).toHaveURL(/\/requests\/p1$/)
    await connect(page)

    await emit(page, 'notificationRaised', { kind: 'merchantResponded', postId: 'p1', threadId: null })
    await emit(page, 'notificationRaised', { kind: 'newMessage', postId: null, threadId: 'thread-closed' })

    await expect(page.getByText(NEW_MESSAGE).first()).toBeVisible() // the later one arrived...
    await expect(page.getByText(RESPONDED)).toHaveCount(0) // ...the one about this request did not
  })
})
