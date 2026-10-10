import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'

// The chat runs on the sample conversations of `chatMock` in dev (app/mocks/chat.ts),
// which is what the dev server used by Playwright serves. Three threads per account:
//   thread-feed-4  — a long history (47 messages for a buyer), 2 unread, active request
//   thread-closed  — a short one, the request is closed (still writable)
//   thread-locked  — locked: nobody can write
// Two sessionStorage hooks play the other side: `gk:mock-chat-incoming` queues the
// counterpart's messages, `gk:mock-chat-fail-send` makes sending fail.

const LAST_BUYER_VIEW = 'Czekamy na Pana, odłożę słuchawki.'

// The Nuxt devtools overlay (dev server only) can sit on top of the bottom navigation.
async function hideDevtools(page: Page) {
  await page.addStyleTag({ content: '#nuxt-devtools-container { display: none !important; }' })
}

async function openChats(page: Page) {
  await page.getByRole('link', { name: /Czaty/ }).click()
  await expect(page).toHaveURL('/chat')
}

async function openThread(page: Page, counterpart: string) {
  await page.getByTestId('chat-thread-row').filter({ hasText: counterpart }).click()
  await expect(page.getByTestId('chat-counterpart')).toHaveText(counterpart)
}

const input = (page: Page) => page.getByRole('textbox', { name: 'Wiadomość' })
const messages = (page: Page) => page.getByTestId('chat-message')

async function write(page: Page, text: string) {
  await input(page).fill(text)
  await input(page).press('Enter')
}

test.describe('Chat — buyer', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await hideDevtools(page)
  })

  test('the navigation shows the unread count, the inbox lists the threads', async ({ page }) => {
    await expect(page.getByRole('link', { name: /Czaty/ }).getByTestId('nav-badge')).toHaveText('2')

    await openChats(page)

    const rows = page.getByTestId('chat-thread-row')
    await expect(rows).toHaveCount(3)

    const long = rows.filter({ hasText: 'Audio Shop Kraków' })
    await expect(long).toContainText('Słuchawki studyjne Beyerdynamic DT 770 Pro')
    await expect(long).toContainText(LAST_BUYER_VIEW)
    await expect(long.getByTestId('chat-thread-unread')).toHaveText('2')

    const closed = rows.filter({ hasText: 'Studio Sklep' })
    await expect(closed).toContainText(/Ty:\s*Dziękuję/)
    await expect(closed.getByTestId('chat-thread-unread')).toHaveCount(0)
    await expect(rows.filter({ hasText: 'Muzyczny Raj' })).toContainText('Zablokowana')
  })

  test('opening a thread shows the newest messages, tells the sides apart and clears the unread count', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Audio Shop Kraków')

    await expect(page.getByText(LAST_BUYER_VIEW)).toBeInViewport()
    await expect(messages(page).last()).toHaveAttribute('data-own', 'false')
    await expect(messages(page).filter({ hasText: 'Świetnie, mogę odebrać jutro po południu?' })).toHaveAttribute('data-own', 'true')
    await expect(page.getByTestId('chat-request-link')).toHaveAttribute('href', '/requests/feed-4')

    await expect(page.getByRole('link', { name: /Czaty/ }).getByTestId('nav-badge')).toHaveCount(0)
    await page.getByTestId('chat-back').click()
    await expect(page.getByTestId('chat-thread-unread')).toHaveCount(0)
  })

  test('older messages load when the top is reached and keep the view where it was', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Audio Shop Kraków')
    await expect(messages(page)).toHaveCount(30)
    await expect(page.getByText(LAST_BUYER_VIEW)).toBeInViewport()

    await page.getByTestId('chat-messages').evaluate((el) => { el.scrollTop = 0 })

    await expect(messages(page)).toHaveCount(47)
    await expect(page.getByTestId('chat-older')).toHaveCount(0)
    // What was at the top stays right below the new page, not thrown to the very top.
    await expect(messages(page).nth(17)).toBeInViewport()
    await expect(page.getByText('Dzień dobry, czy słuchawki DT 770 Pro')).toHaveCount(1)
  })

  test('older messages can also be requested with the button', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Audio Shop Kraków')
    await expect(messages(page)).toHaveCount(30)

    await page.getByRole('button', { name: 'Pokaż starsze wiadomości' }).dispatchEvent('click')

    await expect(messages(page)).toHaveCount(47)
  })

  test('sending a message shows it, clears the box and updates the inbox preview', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')

    await write(page, 'Czy możecie wysłać kurierem?')

    await expect(messages(page).last()).toContainText('Czy możecie wysłać kurierem?')
    await expect(messages(page).last()).toHaveAttribute('data-own', 'true')
    await expect(messages(page).last()).toHaveAttribute('data-state', 'sent')
    await expect(input(page)).toHaveValue('')

    await page.getByTestId('chat-back').click()
    await expect(page.getByTestId('chat-thread-row').first()).toContainText(/Ty:\s*Czy możecie wysłać kurierem\?/)
  })

  test('an empty message cannot be sent, a too long one shows the limit', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')

    await expect(page.getByRole('button', { name: 'Wyślij' })).toBeDisabled()
    await input(page).fill('   ')
    await expect(page.getByRole('button', { name: 'Wyślij' })).toBeDisabled()

    await input(page).fill('a'.repeat(2001))
    await expect(page.getByRole('alert')).toContainText('Wiadomość może mieć najwyżej 2000 znaków.')
    await expect(page.getByRole('button', { name: 'Wyślij' })).toBeDisabled()
    await expect(messages(page)).toHaveCount(3)
  })

  test('a locked thread explains why nobody can write', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Muzyczny Raj')

    await expect(page.getByTestId('chat-locked')).toContainText('Ta rozmowa jest zablokowana')
    await expect(input(page)).toHaveCount(0)
  })

  test('a closed request is mentioned, the conversation continues', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')

    await expect(page.getByTestId('chat-request-inactive')).toContainText('Zamknięte')
    await expect(input(page)).toBeVisible()
  })

  test('a message from the other side arrives while the thread is open', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')

    await page.evaluate(() => sessionStorage.setItem(
      'gk:mock-chat-incoming',
      JSON.stringify([{ threadId: 'thread-closed', body: 'Wysyłamy kurierem jeszcze dziś.' }]),
    ))

    await expect(messages(page).last()).toContainText('Wysyłamy kurierem jeszcze dziś.', { timeout: 10_000 })
    await expect(messages(page).last()).toHaveAttribute('data-own', 'false')
  })

  test('a message that failed to send stays, and can be retried', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await page.evaluate(() => sessionStorage.setItem('gk:mock-chat-fail-send', '1'))

    await write(page, 'Halo, jest ktoś?')

    const failed = messages(page).last()
    await expect(failed).toHaveAttribute('data-state', 'failed')
    await expect(failed).toContainText('Nie wysłano')

    await page.evaluate(() => sessionStorage.removeItem('gk:mock-chat-fail-send'))
    await failed.getByRole('button', { name: 'Ponów' }).click()

    await expect(messages(page).last()).toHaveAttribute('data-state', 'sent')
    await expect(messages(page).filter({ hasText: 'Halo, jest ktoś?' })).toHaveCount(1)
  })

  test('a failed message can be discarded', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Studio Sklep')
    await page.evaluate(() => sessionStorage.setItem('gk:mock-chat-fail-send', '1'))

    await write(page, 'Halo, jest ktoś?')
    await messages(page).last().getByRole('button', { name: 'Usuń' }).click()

    await expect(messages(page)).toHaveCount(3)
  })

  test('an unknown conversation says it was not found', async ({ page }) => {
    await openChats(page)
    await expect(page.getByTestId('chat-thread-row')).toHaveCount(3)

    // Client-side navigation: the session lives in memory.
    await page.evaluate(() => {
      const nuxt = (window as unknown as { useNuxtApp: () => { $router: { push: (to: string) => void } } }).useNuxtApp()
      nuxt.$router.push('/chat/does-not-exist')
    })

    await expect(page.getByTestId('chat-not-found')).toContainText('Nie znaleziono rozmowy.')
  })
})

test.describe('Chat — merchant', () => {
  test.beforeEach(async ({ page }) => {
    // An onboarded merchant, otherwise the onboarding guard takes over.
    await mockApi(page, [
      {
        method: 'GET',
        path: '/api/merchant/me',
        respond: () => ({
          json: {
            merchantId: 'm1',
            name: 'Audio Shop Kraków',
            description: null,
            branch: { id: 'b1', displayName: 'Rynek', latitude: 50.06, longitude: 19.94, addressDisplayName: null, phone: null, website: null },
          },
        }),
      },
    ])

    await loginAs(page, 'Merchant')
    await expect(page).toHaveURL('/feed')
    await hideDevtools(page)
  })

  test('the inbox shows the buyers, the unread ones are the buyer\'s messages', async ({ page }) => {
    await expect(page.getByRole('link', { name: /Czaty/ }).getByTestId('nav-badge')).toHaveText('2')

    await openChats(page)

    const row = page.getByTestId('chat-thread-row').filter({ hasText: 'Kasia' })
    await expect(row).toContainText('Słuchawki studyjne Beyerdynamic DT 770 Pro')
    await expect(row).toContainText('Będę przed 16:00.')
    await expect(row.getByTestId('chat-thread-unread')).toHaveText('2')
  })

  test('a conversation shows the buyer as the other side and links to the request in the feed', async ({ page }) => {
    await openChats(page)
    await openThread(page, 'Kasia')

    await expect(page.getByText('Będę przed 16:00.')).toBeInViewport()
    await expect(messages(page).last()).toHaveAttribute('data-own', 'false')
    await expect(page.getByTestId('chat-request-link')).toHaveAttribute('href', '/feed/feed-4')

    await write(page, 'Tak, słuchawki czekają na Pana.')
    await expect(messages(page).last()).toContainText('Tak, słuchawki czekają na Pana.')
    await expect(messages(page).last()).toHaveAttribute('data-own', 'true')
  })

  test('the chat link of a response in the feed opens the conversation', async ({ page }) => {
    await page.getByRole('link', { name: 'Interfejs audio USB do domowego studia' }).click()
    await expect(page).toHaveURL('/feed/feed-2')

    await page.getByRole('button', { name: 'Mam to' }).click()
    await page.getByTestId('open-thread').click()

    await expect(page).toHaveURL('/chat/thread-feed-2')
    await expect(page.getByTestId('chat-counterpart')).toHaveText('Anna')
    await expect(input(page)).toBeVisible()

    await write(page, 'Dzień dobry, mamy ten interfejs.')
    await expect(messages(page)).toHaveCount(1)
  })
})
