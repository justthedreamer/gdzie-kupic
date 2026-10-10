import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'

// The merchant feed follows the server's events. The mock Playwright config runs with
// `realtimeMock` (`window.__realtime` plays the server) and `merchantFeedMock` (the
// in-memory feed). Two sessionStorage hooks of the feed mock play the server's side:
// `gk:mock-hidden-posts` (not notified yet) and `gk:mock-ended-posts` (closed, gone from the
// list and the counters, still readable by id). New requests of the sample data: feed-1, -2,
// -3 and -7 (the navigation badge shows 4).

const setIds = (page: Page, key: string, ids: string[]) =>
  page.evaluate(([k, v]) => sessionStorage.setItem(k!, JSON.stringify(v)), [key, ids] as const)
const emit = (page: Page, name: string, payload?: unknown) =>
  page.evaluate(([n, p]) => window.__realtime!.emit(n as string, p), [name, payload])

const card = (page: Page, title: string) => page.getByTestId('feed-card').filter({ hasText: title })
const badge = (page: Page) => page.locator('a[href="/feed"]:visible').getByTestId('nav-badge')

const MICROPHONE = 'Szukam mikrofonu Shure SM7B'
const INTERFACE = 'Interfejs audio USB do domowego studia'
const HEADPHONES = 'Słuchawki studyjne Beyerdynamic DT 770 Pro'

test.describe('Merchant feed — real-time', () => {
  test.beforeEach(async ({ page }) => {
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
    // The mock hooks live in sessionStorage; start with feed-1 not yet notified.
    await page.addInitScript(() => sessionStorage.setItem('gk:mock-hidden-posts', JSON.stringify(['feed-1'])))

    await loginAs(page, 'Merchant')
    await expect(page).toHaveURL('/feed')
    await expect(card(page, INTERFACE)).toBeVisible()
  })

  test('a new request appears in the feed and the counter, keeping the active tab', async ({ page }) => {
    await expect(card(page, MICROPHONE)).toHaveCount(0)
    await expect(badge(page)).toHaveText('3')

    await setIds(page, 'gk:mock-hidden-posts', [])
    await emit(page, 'postAdded', { postId: 'feed-1' })

    await expect(card(page, MICROPHONE)).toBeVisible()
    await expect(badge(page)).toHaveText('4')
    await expect(page.getByRole('tab', { name: /Nowe/ })).toHaveAttribute('aria-selected', 'true')
  })

  test('a new request keeps the other tab the merchant is on', async ({ page }) => {
    await page.getByRole('tab', { name: /Odpowiedziane/ }).click()
    await expect(card(page, HEADPHONES)).toBeVisible()

    await setIds(page, 'gk:mock-hidden-posts', [])
    await emit(page, 'postAdded', { postId: 'feed-1' })

    await expect(badge(page)).toHaveText('4')
    await expect(page.getByRole('tab', { name: /Odpowiedziane/ })).toHaveAttribute('aria-selected', 'true')
    await expect(card(page, HEADPHONES)).toBeVisible()
    await expect(card(page, MICROPHONE)).toHaveCount(0) // a new one belongs to the "New" tab
  })

  test('a request that ended disappears from the feed and the counter', async ({ page }) => {
    await expect(card(page, INTERFACE)).toBeVisible()

    await setIds(page, 'gk:mock-ended-posts', ['feed-2'])
    await emit(page, 'postRemoved', { postId: 'feed-2' })

    await expect(card(page, INTERFACE)).toHaveCount(0)
    await expect(badge(page)).toHaveText('2')
  })

  test('an open request that ended shows a notice, locks the answers and keeps the chat link', async ({ page }) => {
    await page.getByRole('tab', { name: /Odpowiedziane/ }).click()
    await card(page, HEADPHONES).getByRole('link', { name: HEADPHONES }).click()
    await expect(page).toHaveURL('/feed/feed-4')
    await expect(page.getByTestId('closed-notice')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Mam to' })).toBeEnabled()
    await expect(page.getByTestId('open-thread')).toBeVisible()

    await setIds(page, 'gk:mock-ended-posts', ['feed-4'])
    await emit(page, 'postRemoved', { postId: 'feed-4' })

    await expect(page.getByTestId('closed-notice')).toContainText('Zamknięte')
    for (const name of ['Mam to', 'Mogę mieć', 'Mogę zamówić', 'Nie pomogę']) {
      await expect(page.getByRole('button', { name })).toBeDisabled()
    }
    await expect(page.getByTestId('open-thread')).toHaveAttribute('href', '/chat/thread-feed-4')
    await expect(page.getByRole('heading', { name: HEADPHONES })).toBeVisible()
  })

  test('the event of another request does not touch an open one', async ({ page }) => {
    await card(page, INTERFACE).getByRole('link', { name: INTERFACE }).click()
    await expect(page).toHaveURL('/feed/feed-2')

    await setIds(page, 'gk:mock-ended-posts', ['feed-3'])
    await emit(page, 'postRemoved', { postId: 'feed-3' })

    await expect(badge(page)).toHaveText('2')
    await expect(page.getByTestId('closed-notice')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Mam to' })).toBeEnabled()
  })

  test('a resync after a reconnect refetches the feed and the counter', async ({ page }) => {
    await setIds(page, 'gk:mock-hidden-posts', [])
    await setIds(page, 'gk:mock-ended-posts', ['feed-3'])

    await page.evaluate(() => window.__realtime!.setState('connected')) // no event, only the resync

    await expect(card(page, MICROPHONE)).toBeVisible()
    await expect(page.getByTestId('feed-card').filter({ hasText: 'Pilnie: kable XLR' })).toHaveCount(0)
    await expect(badge(page)).toHaveText('3')
  })

  test('a resync tells an open request that it ended meanwhile', async ({ page }) => {
    await card(page, INTERFACE).getByRole('link', { name: INTERFACE }).click()
    await expect(page).toHaveURL('/feed/feed-2')
    await expect(page.getByRole('button', { name: 'Mam to' })).toBeEnabled()

    await setIds(page, 'gk:mock-ended-posts', ['feed-2'])
    await page.evaluate(() => window.__realtime!.setState('connected'))

    await expect(page.getByTestId('closed-notice')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Mam to' })).toBeDisabled()
  })
})
