import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, type MockHandler } from './support/api-mock'
import { hoursFromNow, postPath, sampleStatus, samplePost } from './support/posts'

// Runs against the API contract (planning/phase-4-post-lifecycle-matching.md § API contract)
// with mocked responses; the real-API run comes when the service is ready.

type Json = Record<string, unknown>

interface Server {
  active: Json[]
  ended: Json[]
  post: Json
  status: Json | (() => Json)
  calls: string[]
  fulfil?: MockHandler['respond']
  close?: MockHandler['respond']
  longLived?: MockHandler['respond']
}

function createServer(overrides: Partial<Server> = {}): Server {
  return {
    active: [samplePost()],
    ended: [],
    post: samplePost(),
    status: sampleStatus(),
    calls: [],
    ...overrides,
  }
}

function handlers(server: Server): MockHandler[] {
  const endPost = (status: string) => {
    server.post = { ...server.post, status }
    return { status: 204 }
  }

  return [
    {
      method: 'GET',
      path: '/api/posts',
      respond: ({ url }) => ({ json: url.searchParams.get('scope') === 'ended' ? server.ended : server.active }),
    },
    { method: 'GET', path: postPath('/status'), respond: () => ({ json: typeof server.status === 'function' ? server.status() : server.status }) },
    {
      method: 'POST',
      path: postPath('/fulfil'),
      respond: (req) => {
        server.calls.push('fulfil')
        return server.fulfil ? server.fulfil(req) : endPost('Fulfilled')
      },
    },
    {
      method: 'POST',
      path: postPath('/close'),
      respond: (req) => {
        server.calls.push('close')
        return server.close ? server.close(req) : endPost('Closed')
      },
    },
    {
      method: 'POST',
      path: postPath('/long-lived'),
      respond: (req) => {
        server.calls.push('long-lived')
        if (server.longLived) return server.longLived(req)
        server.post = { ...server.post, isLongLived: true, expiresAt: hoursFromNow(14 * 24) }
        return { json: server.post }
      },
    },
    { method: 'GET', path: postPath(), respond: () => ({ json: server.post }) },
  ]
}

// Client-side navigation only: a full reload would drop the in-memory session.
async function goToRequests(page: Page) {
  const isMobile = (page.viewportSize()?.width ?? 0) < 1024
  const nav = page.getByRole('navigation', { name: isMobile ? 'Nawigacja' : 'Nawigacja główna' })

  await nav.getByRole('link', { name: 'Zapytania' }).click()
  await expect(page).toHaveURL('/requests')
}

async function openList(page: Page, server: Server) {
  await mockApi(page, handlers(server))
  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')
  await goToRequests(page)
}

async function openDetail(page: Page, server: Server, title = 'Rower górski') {
  await openList(page, server)
  await page.getByRole('link', { name: new RegExp(title) }).click()
  await expect(page).toHaveURL(/\/requests\/p1$/)
  // A CSS locator: an open dialog hides the page behind it from role queries.
  await expect(page.locator('h1', { hasText: title })).toBeVisible()
}

test.describe('Request list', () => {
  test('shows active requests with status, notified count, category, tag and remaining time', async ({ page }) => {
    await openList(page, createServer({ active: [samplePost({ isUrgent: true, notifiedCount: 7 })] }))

    const card = page.getByTestId('request-card')
    await expect(card).toHaveCount(1)
    await expect(card.getByRole('heading', { name: 'Rower górski' })).toBeVisible()
    await expect(card.getByText('Aktywne')).toBeVisible()
    await expect(card.getByText('Pilne')).toBeVisible()
    await expect(card.getByText('Sport · Rowery')).toBeVisible()
    await expect(card.getByText('Powiadomiono: 7')).toBeVisible()
    await expect(card.getByTestId('post-remaining')).toContainText('Zostało')
  })

  test('the Ended tab lists finished requests', async ({ page }) => {
    await openList(page, createServer({
      active: [],
      ended: [samplePost({ id: 'p9', title: 'Stary rower', status: 'Closed', isLongLived: true })],
    }))

    await expect(page.getByText('Nie masz aktywnych zapytań')).toBeVisible()
    await page.getByRole('tab', { name: 'Zakończone' }).click()

    const card = page.getByTestId('request-card')
    await expect(card.getByRole('heading', { name: 'Stary rower' })).toBeVisible()
    await expect(card.getByText('Zamknięte')).toBeVisible()
    await expect(card.getByText('Długo aktywne')).toBeVisible()
    await expect(card.getByTestId('post-remaining')).toHaveCount(0)
  })

  test('has an empty state for each tab', async ({ page }) => {
    await openList(page, createServer({ active: [], ended: [] }))

    await expect(page.getByText('Nie masz aktywnych zapytań')).toBeVisible()
    await page.getByRole('tab', { name: 'Zakończone' }).click()
    await expect(page.getByText('Brak zakończonych zapytań')).toBeVisible()
  })

  test('offers a retry when loading fails', async ({ page }) => {
    const server = createServer()
    let fail = true
    await mockApi(page, [
      { method: 'GET', path: '/api/posts', respond: () => (fail ? { status: 500 } : { json: server.active }) },
      ...handlers(server).slice(1),
    ])
    await loginAs(page, 'Buyer')
    await goToRequests(page)

    await expect(page.getByText('Nie udało się wczytać zapytań.')).toBeVisible()
    fail = false
    await page.getByRole('button', { name: 'Spróbuj ponownie' }).click()
    await expect(page.getByTestId('request-card')).toHaveCount(1)
  })
})

test.describe('Request detail', () => {
  test('shows the request and the status panel with the notified count', async ({ page }) => {
    await openDetail(page, createServer({ status: sampleStatus({ notifiedCount: 5 }) }))

    await expect(page.getByText('Rama M, najlepiej aluminiowa.')).toBeVisible()
    await expect(page.getByText('Sport · Rowery')).toBeVisible()
    await expect(page.getByText('10 km')).toBeVisible()
    await expect(page.getByTestId('notified-count')).toHaveText('5')
    await expect(page.getByText('Szukamy sprzedawców…')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Odpowiedzi sprzedawców' })).toBeVisible()
  })

  test('shows "looking for merchants" while matching is pending and refreshes to the result', async ({ page }) => {
    // The home page loads the status of the same request before the detail does.
    let pending = true
    const server = createServer({
      post: samplePost({ notificationDispatchStatus: 'Pending' }),
      active: [samplePost({ notificationDispatchStatus: 'Pending' })],
      status: () => (pending
        ? sampleStatus({ notificationDispatchStatus: 'Pending', notifiedCount: 0 })
        : sampleStatus({ notifiedCount: 4 })),
    })
    await openDetail(page, server)

    await expect(page.getByText('Szukamy sprzedawców…')).toBeVisible()

    // The next refresh comes after 5 s.
    pending = false
    await expect(page.getByTestId('notified-count')).toHaveText('4', { timeout: 15_000 })
    await expect(page.getByText('Szukamy sprzedawców…')).toHaveCount(0)
  })

  test('stops polling when leaving the page', async ({ page }) => {
    let calls = 0
    const server = createServer({
      status: () => {
        calls++
        return sampleStatus({ notificationDispatchStatus: 'Pending', notifiedCount: 0 })
      },
    })
    await openDetail(page, server)
    await expect(page.getByText('Szukamy sprzedawców…')).toBeVisible()

    await page.getByRole('link', { name: 'Wszystkie zapytania' }).click()
    await expect(page).toHaveURL('/requests')
    const after = calls
    await page.waitForTimeout(6_500)

    expect(calls).toBe(after)
  })

  test('"Found" asks for confirmation, updates the request and removes the actions', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)

    await page.getByRole('button', { name: 'Znalazłem' }).click()
    const dialog = page.getByRole('dialog')
    await expect(dialog.getByText('Oznaczyć jako znalezione?')).toBeVisible()

    await dialog.getByRole('button', { name: 'Anuluj' }).click()
    await expect(dialog).toBeHidden()
    expect(server.calls).toEqual([])

    await page.getByRole('button', { name: 'Znalazłem' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Tak, znalazłem' }).click()

    await expect(page.getByTestId('post-status')).toHaveText('Znalezione')
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Zamknij zapytanie' })).toHaveCount(0)
    expect(server.calls).toEqual(['fulfil'])
  })

  test('"Close" asks for confirmation and closes the request', async ({ page }) => {
    const server = createServer()
    await openDetail(page, server)

    await page.getByRole('button', { name: 'Zamknij zapytanie' }).click()
    await expect(page.getByRole('dialog').getByText('Zamknąć zapytanie?')).toBeVisible()
    await page.getByRole('dialog').getByRole('button', { name: 'Zamknij zapytanie' }).click()

    await expect(page.getByTestId('post-status')).toHaveText('Zamknięte')
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toHaveCount(0)
    expect(server.calls).toEqual(['close'])
  })

  test('a 409 shows a clear message and the real state', async ({ page }) => {
    const server = createServer()
    server.close = () => {
      server.post = { ...server.post, status: 'Expired' }
      return { status: 409, json: { title: 'Conflict' } }
    }
    await openDetail(page, server)

    await page.getByRole('button', { name: 'Zamknij zapytanie' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Zamknij zapytanie' }).click()

    await expect(page.getByTestId('request-notice')).toContainText('To zapytanie nie jest już aktywne')
    await expect(page.getByTestId('post-status')).toHaveText('Wygasłe')
    await expect(page.getByRole('button', { name: 'Znalazłem' })).toHaveCount(0)
  })

  test('another failure keeps the confirmation open with an error', async ({ page }) => {
    const server = createServer()
    server.fulfil = () => ({ status: 500 })
    await openDetail(page, server)

    await page.getByRole('button', { name: 'Znalazłem' }).click()
    await page.getByRole('dialog').getByRole('button', { name: 'Tak, znalazłem' }).click()

    await expect(page.getByRole('dialog').getByText('Nie udało się wykonać akcji.')).toBeVisible()
    await expect(page.getByTestId('post-status')).toHaveText('Aktywne')
  })

  test('an unknown request shows a not-found state', async ({ page }) => {
    const server = createServer()
    await mockApi(page, [
      ...handlers(server).slice(0, -1),
      { method: 'GET', path: postPath(), respond: () => ({ status: 404 }) },
    ])
    await loginAs(page, 'Buyer')
    await goToRequests(page)
    await page.getByRole('link', { name: /Rower górski/ }).click()

    await expect(page.getByText('Nie znaleziono zapytania')).toBeVisible()
  })

  test('shows an error with a retry when the request cannot be loaded', async ({ page }) => {
    const server = createServer()
    let fail = true
    await mockApi(page, [
      ...handlers(server).slice(0, -1),
      { method: 'GET', path: postPath(), respond: () => (fail ? { status: 500 } : { json: server.post }) },
    ])
    await loginAs(page, 'Buyer')
    await goToRequests(page)
    await page.getByRole('link', { name: /Rower górski/ }).click()

    await expect(page.getByText('Nie udało się wczytać zapytania.')).toBeVisible()
    fail = false
    await page.getByRole('button', { name: 'Spróbuj ponownie' }).click()
    await expect(page.locator('h1', { hasText: 'Rower górski' })).toBeVisible()
  })
})

test.describe('Zero-match popup', () => {
  const zeroMatch = { status: sampleStatus({ notifiedCount: 0, isZeroMatch: true }) }

  test('extends the request on confirmation and shows the badge and new expiry', async ({ page }) => {
    const server = createServer(zeroMatch)
    await openDetail(page, server)

    const dialog = page.getByRole('dialog')
    await expect(dialog.getByText('Nikogo jeszcze nie znaleźliśmy')).toBeVisible()
    const before = await page.getByTestId('post-expires').innerText()

    await dialog.getByRole('button', { name: 'Przedłuż do 14 dni' }).click()

    await expect(dialog).toBeHidden()
    await expect(page.getByText('Długo aktywne')).toBeVisible()
    await expect(page.getByTestId('post-expires')).not.toHaveText(before)
    expect(server.calls).toEqual(['long-lived'])
  })

  test('is not shown again after being dismissed', async ({ page }) => {
    const server = createServer(zeroMatch)
    await openDetail(page, server)

    await page.getByRole('dialog').getByRole('button', { name: 'Nie teraz' }).click()
    await expect(page.getByRole('dialog')).toBeHidden()

    await page.getByRole('link', { name: 'Wszystkie zapytania' }).click()
    await page.getByRole('link', { name: /Rower górski/ }).click()
    await expect(page.locator('h1', { hasText: 'Rower górski' })).toBeVisible()
    await expect(page.getByText('Żaden sprzedawca w okolicy jeszcze nie pasuje')).toBeVisible()
    await expect(page.getByRole('dialog')).toHaveCount(0)
    expect(server.calls).toEqual([])
  })

  for (const [name, post] of [['an urgent request', samplePost({ isUrgent: true })], ['an already long-lived request', samplePost({ isLongLived: true })]] as const) {
    test(`is not offered for ${name}`, async ({ page }) => {
      await openDetail(page, createServer({ ...zeroMatch, post, active: [post] }))
      await expect(page.getByRole('dialog')).toHaveCount(0)
    })
  }

  test('is not shown while matching is pending or when merchants were notified', async ({ page }) => {
    const server = createServer({ status: sampleStatus({ notificationDispatchStatus: 'Pending', notifiedCount: 0, isZeroMatch: false }) })
    await openDetail(page, server)

    await expect(page.getByText('Szukamy sprzedawców…')).toBeVisible()
    await expect(page.getByRole('dialog')).toHaveCount(0)
  })

  test('a 409 on extending explains that the request cannot be extended', async ({ page }) => {
    const server = createServer(zeroMatch)
    server.longLived = () => ({ status: 409, json: { title: 'Conflict' } })
    await openDetail(page, server)

    await page.getByRole('dialog').getByRole('button', { name: 'Przedłuż do 14 dni' }).click()

    await expect(page.getByTestId('request-notice')).toContainText('nie można już przedłużyć')
    await expect(page.getByRole('dialog')).toBeHidden()
  })
})
