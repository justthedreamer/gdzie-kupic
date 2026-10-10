import { test, expect, type Page } from '@playwright/test'
import { loginAs } from './support/api-mock'

// Pictures in the chat, on the sample conversations of `chatMock` (see chat.spec.ts). The
// mock "server" applies the server's rules (JPEG / PNG / WebP, 5 MB) and keeps what is sent;
// a picture of the other side comes through the `gk:mock-chat-incoming` hook with a URL that
// the test serves (or breaks) itself.

const PIXEL = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64',
)
const png = (name = 'zdjecie.png') => ({ name, mimeType: 'image/png', buffer: PIXEL })

async function hideDevtools(page: Page) {
  await page.addStyleTag({ content: '#nuxt-devtools-container { display: none !important; }' })
}

async function openStudioThread(page: Page) {
  await page.getByRole('link', { name: /Czaty/ }).click()
  await expect(page).toHaveURL('/chat')
  await page.getByTestId('chat-thread-row').filter({ hasText: 'Studio Sklep' }).click()
  await expect(page.getByTestId('chat-counterpart')).toHaveText('Studio Sklep')
}

const attach = (page: Page) => page.getByTestId('chat-attach-input')
const messages = (page: Page) => page.getByTestId('chat-message')
const send = (page: Page) => page.getByRole('button', { name: 'Wyślij' })

test.describe('Chat — pictures', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('https://files.test/**', route => route.fulfill({ status: 200, contentType: 'image/png', body: PIXEL }))
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
    await hideDevtools(page)
    await openStudioThread(page)
  })

  test('a chosen picture shows a preview that can be removed', async ({ page }) => {
    await expect(page.getByTestId('chat-attachment-preview')).toHaveCount(0)

    await attach(page).setInputFiles(png())
    await expect(page.getByTestId('chat-attachment-preview').getByRole('img', { name: 'Wybrane zdjęcie' })).toBeVisible()
    await expect(send(page)).toBeEnabled()

    await page.getByRole('button', { name: 'Usuń zdjęcie' }).click()
    await expect(page.getByTestId('chat-attachment-preview')).toHaveCount(0)
    await expect(send(page)).toBeDisabled()
  })

  test('a picture without text is sent, shown in the thread and named in the inbox', async ({ page }) => {
    await attach(page).setInputFiles(png())
    await send(page).click()

    const sent = messages(page).last()
    await expect(sent).toHaveAttribute('data-state', 'sent')
    await expect(sent).toHaveAttribute('data-own', 'true')
    await expect(sent.getByTestId('chat-image')).toBeVisible()
    await expect(page.getByTestId('chat-attachment-preview')).toHaveCount(0)
    await expect(messages(page)).toHaveCount(4)

    await page.getByTestId('chat-back').click()
    await expect(page.getByTestId('chat-thread-row').first()).toContainText(/Ty:\s*Zdjęcie/)
  })

  test('a picture with text is sent together', async ({ page }) => {
    await attach(page).setInputFiles(png())
    await page.getByRole('textbox', { name: 'Wiadomość' }).fill('Takie macie?')
    await page.getByRole('textbox', { name: 'Wiadomość' }).press('Enter')

    const sent = messages(page).last()
    await expect(sent).toContainText('Takie macie?')
    await expect(sent.getByTestId('chat-image')).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'Wiadomość' })).toHaveValue('')
  })

  test('a file of another type is refused before sending', async ({ page }) => {
    await attach(page).setInputFiles({ name: 'animacja.gif', mimeType: 'image/gif', buffer: PIXEL })

    await expect(page.getByTestId('chat-attachment-error')).toContainText('JPEG, PNG lub WebP')
    await expect(page.getByTestId('chat-attachment-preview')).toHaveCount(0)
    await expect(send(page)).toBeDisabled()
  })

  test('a file over the limit is refused with the size', async ({ page }) => {
    await attach(page).setInputFiles({ name: 'duze.png', mimeType: 'image/png', buffer: Buffer.alloc(5 * 1024 * 1024 + 1) })

    await expect(page.getByTestId('chat-attachment-error')).toContainText('5 MB')
    await expect(page.getByTestId('chat-attachment-preview')).toHaveCount(0)
  })

  test('a later valid choice replaces the refusal', async ({ page }) => {
    await attach(page).setInputFiles({ name: 'animacja.gif', mimeType: 'image/gif', buffer: PIXEL })
    await expect(page.getByTestId('chat-attachment-error')).toBeVisible()

    await attach(page).setInputFiles(png())

    await expect(page.getByTestId('chat-attachment-error')).toHaveCount(0)
    await expect(page.getByTestId('chat-attachment-preview')).toBeVisible()
  })

  test('a picture of the other side appears while the thread is open and opens enlarged', async ({ page }) => {
    await page.evaluate(() => sessionStorage.setItem(
      'gk:mock-chat-incoming',
      JSON.stringify([{ threadId: 'thread-closed', body: 'Oto zdjęcie', attachmentUrl: 'https://files.test/sluchawki.png' }]),
    ))

    const incoming = messages(page).last()
    await expect(incoming).toContainText('Oto zdjęcie', { timeout: 10_000 })
    await expect(incoming).toHaveAttribute('data-own', 'false')
    await expect(incoming.getByRole('img', { name: 'Zdjęcie od: Studio Sklep' })).toBeVisible()
    await expect.poll(() => incoming.getByTestId('chat-image').evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)

    await incoming.getByRole('button', { name: 'Powiększ zdjęcie' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog.getByTestId('chat-image-large')).toBeVisible()

    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()
  })

  test('a picture that no longer loads shows a fallback and can be loaded again', async ({ page }) => {
    await page.unroute('https://files.test/**')
    await page.route('https://files.test/**', route => route.abort())
    await page.evaluate(() => sessionStorage.setItem(
      'gk:mock-chat-incoming',
      JSON.stringify([{ threadId: 'thread-closed', body: null, attachmentUrl: 'https://files.test/wygasle.png' }]),
    ))

    const incoming = messages(page).last()
    await expect(incoming.getByTestId('chat-image-fallback')).toContainText('Nie udało się wczytać zdjęcia.', { timeout: 10_000 })
    await expect(incoming.getByTestId('chat-image')).toHaveCount(0)

    await page.unroute('https://files.test/**')
    await page.route('https://files.test/**', route => route.fulfill({ status: 200, contentType: 'image/png', body: PIXEL }))
    await incoming.getByRole('button', { name: 'Wczytaj ponownie' }).click()

    await expect(incoming.getByTestId('chat-image')).toBeVisible()
    await expect(incoming.getByTestId('chat-image-fallback')).toHaveCount(0)
  })
})
