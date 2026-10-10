import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, openShellNav } from './support/api-mock'

// The Merchant feed runs on mocked data in dev (`merchantFeedMock`), which is
// what the dev server used by Playwright serves. The mock emulates the server:
// filters, sorting, cursor paging (4 per page) and the summary.

/** Scrolls to the end of the page until the infinite scroll has loaded everything. */
async function scrollToTheEnd(page: Page, total: number) {
  const cards = page.getByTestId('feed-card')
  await expect(async () => {
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight))
    await expect(cards).toHaveCount(total, { timeout: 1000 })
  }).toPass()
}

test.describe('Merchant requests feed', () => {
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
  })

  test('is the default page after login: new requests, urgent first', async ({ page, isMobile }) => {
    // The page title is the mobile top bar on small screens and the page heading on desktop.
    if (isMobile) {
      await expect(page.getByRole('banner').getByText('Zapytania w okolicy')).toBeVisible()
    }
    else {
      await expect(page.getByRole('heading', { name: 'Zapytania w okolicy', level: 1 })).toBeVisible()
    }

    const cards = page.getByTestId('feed-card')
    await expect(cards).toHaveCount(4)
    await expect(cards.first().getByRole('heading')).toHaveText('Pilnie: kable XLR 5 m, 4 sztuki')
    await expect(cards.first().getByText('Pilne')).toBeVisible()
  })

  test('responding from a card moves it from New to Responded', async ({ page }) => {
    const cards = page.getByTestId('feed-card')
    const title = 'Szukam mikrofonu Shure SM7B'

    await cards.filter({ hasText: title }).getByRole('button', { name: 'Mam to' }).click()

    await expect(cards).toHaveCount(3)
    await expect(page.getByText(title)).toHaveCount(0)

    await page.getByRole('tab', { name: /Odpowiedziane/ }).click()
    await expect(cards).toHaveCount(4)
    const answered = cards.filter({ hasText: title })
    await expect(answered).toBeVisible()
    await expect(answered.getByRole('button', { name: 'Mam to' })).toHaveAttribute('aria-pressed', 'true')
  })

  test('the response can be changed on the details page and stays after going back', async ({ page }) => {
    const title = 'Interfejs audio USB do domowego studia'

    await page.getByRole('link', { name: title }).click()
    await expect(page).toHaveURL('/feed/feed-2')
    await expect(page.getByRole('heading', { name: title, level: 1 })).toBeVisible()
    await expect(page.getByTestId('current-response')).toContainText('Nie odpowiedziano')

    await page.getByRole('button', { name: 'Mogę mieć' }).click()
    await expect(page.getByTestId('current-response')).toHaveText('Mogę mieć')

    await page.getByRole('button', { name: 'Mogę zamówić' }).click()
    await expect(page.getByTestId('current-response')).toHaveText('Mogę zamówić')
    await expect(page.getByRole('button', { name: 'Mogę zamówić' })).toHaveAttribute('aria-pressed', 'true')

    await page.getByRole('button', { name: 'Nie pomogę' }).click()
    await expect(page.getByTestId('current-response')).toHaveText('Nie pomogę')

    await page.goBack()
    await expect(page).toHaveURL('/feed')
    await expect(page.getByText(title)).toHaveCount(0)
  })

  test('filters narrow the list', async ({ page, isMobile }) => {
    if (isMobile) await page.getByRole('button', { name: 'Filtry' }).click()

    await page.getByRole('tab', { name: /Wszystkie/ }).click()
    const cards = page.getByTestId('feed-card')
    await scrollToTheEnd(page, 7)

    await page.getByRole('combobox', { name: 'Maksymalna odległość' }).click()
    await page.getByRole('option', { name: '5 km', exact: true }).click()

    await expect(cards).toHaveCount(3)
  })

  test('the category filter is applied by the server', async ({ page, isMobile }) => {
    if (isMobile) await page.getByRole('button', { name: 'Filtry' }).click()

    await page.getByRole('tab', { name: /Wszystkie/ }).click()
    await page.getByRole('combobox', { name: 'Kategoria' }).click()
    await page.getByRole('option', { name: 'Instrumenty', exact: true }).click()

    const cards = page.getByTestId('feed-card')
    await expect(cards).toHaveCount(2)
    await expect(cards.getByText('Instrumenty')).toHaveCount(2)
  })

  test('sorting by distance reloads the list from the nearest request', async ({ page, isMobile }) => {
    if (isMobile) await page.getByRole('button', { name: 'Filtry' }).click()

    await page.getByRole('tab', { name: /Wszystkie/ }).click()
    const cards = page.getByTestId('feed-card')
    await expect(cards.first().getByRole('heading')).toHaveText('Pilnie: kable XLR 5 m, 4 sztuki')

    await page.getByRole('combobox', { name: 'Sortowanie' }).click()
    await page.getByRole('option', { name: 'Najbliższe' }).click()

    await expect(cards.first().getByRole('heading')).toHaveText('Wzmacniacz gitarowy lampowy do 50 W')
  })

  test('scrolling to the end loads the next page, without duplicates, and then stops', async ({ page }) => {
    await page.getByRole('tab', { name: /Wszystkie/ }).click()
    const cards = page.getByTestId('feed-card')
    await expect(cards.first()).toBeVisible()

    await scrollToTheEnd(page, 7)

    await expect(page.getByTestId('feed-more')).toHaveCount(0)
    const titles = await cards.getByRole('heading').allTextContents()
    expect(new Set(titles).size).toBe(7)
  })

  test('the navigation badge counts the new requests and follows responses', async ({ page }) => {
    const badge = page.locator('[data-testid="nav-badge"]:visible')
    await expect(badge).toHaveText('4')

    await page.getByTestId('feed-card').first().getByRole('button', { name: 'Mam to' }).click()

    await expect(badge).toHaveText('3')
    await expect(page.getByRole('tab', { name: /Odpowiedziane/ })).toContainText('4')
  })

  test('shop settings opens the subscriptions page in the same shell', async ({ page }) => {
    await openShellNav(page, 'Ustawienia sklepu')

    await expect(page).toHaveURL('/merchant/subscriptions')
  })

  test('desktop shows the sidebar, mobile shows bottom tabs without a centre action', async ({ page, isMobile }) => {
    if (isMobile) {
      const tabs = page.getByRole('navigation', { name: 'Nawigacja' })
      await expect(tabs).toBeVisible()
      await expect(tabs.getByRole('link', { name: 'Zapytania' })).toHaveAttribute('href', '/feed')
      await expect(tabs.getByRole('button', { name: 'Moje odpowiedzi' })).toBeDisabled()
      await expect(tabs.getByRole('link', { name: 'Nowe zapytanie' })).toHaveCount(0)
    }
    else {
      const sidebar = page.getByRole('navigation', { name: 'Nawigacja główna' })
      await expect(sidebar.getByRole('link', { name: 'Zapytania' })).toBeVisible()
      await expect(sidebar.getByRole('button', { name: /Moje odpowiedzi/ })).toBeDisabled()
      await expect(sidebar.getByRole('link', { name: 'Ustawienia sklepu' })).toHaveAttribute('href', '/merchant/subscriptions')
      await expect(page.getByText('Audio Shop Kraków')).toBeVisible()
    }
  })
})
