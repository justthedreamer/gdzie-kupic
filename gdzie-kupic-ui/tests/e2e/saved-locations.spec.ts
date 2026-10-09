import { test, expect } from '@playwright/test'
import { loginAs, mockApi, openBuyerNav, type MockHandler } from './support/api-mock'

interface Saved { id: string, displayName: string, latitude: number, longitude: number, createdAt: string }

const home: Saved = { id: 's1', displayName: 'Dom', latitude: 50.0647, longitude: 19.945, createdAt: '2026-01-01T00:00:00Z' }

async function openSavedLocations(page: import('@playwright/test').Page, items: Saved[], extra: MockHandler[] = []) {
  await mockApi(page, [
    { method: 'GET', path: '/api/saved-locations', respond: () => ({ json: items }) },
    ...extra,
  ])
  await loginAs(page, 'Buyer')
  await openBuyerNav(page, 'Moje lokalizacje')
  await expect(page).toHaveURL('/saved-locations')
}

test('shows the empty state and the saved list', async ({ page }) => {
  await openSavedLocations(page, [])
  await expect(page.getByText('Nie masz jeszcze zapisanych lokalizacji.')).toBeVisible()
})

test('lists saved locations with coordinates', async ({ page }) => {
  await openSavedLocations(page, [home])
  await expect(page.getByText('Dom', { exact: true })).toBeVisible()
  await expect(page.getByText('50.06470, 19.94500')).toBeVisible()
})

test('adds a location by typed address', async ({ page }) => {
  const items: Saved[] = []
  let sent: Record<string, unknown> | null = null

  await openSavedLocations(page, items, [
    {
      method: 'POST',
      path: '/api/saved-locations',
      respond: ({ body }) => {
        sent = body
        const created = { ...home, id: 's2', displayName: String(body?.displayName) }
        items.push(created)
        return { status: 201, json: created }
      },
    },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Praca')
  await page.getByRole('button', { name: 'Wpisz adres' }).click()
  await page.getByLabel('Adres').fill('Rynek Główny 1, Kraków')
  await page.getByRole('button', { name: 'Zapisz lokalizację' }).click()

  await expect(page.getByText('Praca', { exact: true })).toBeVisible()
  expect(sent).toEqual({ displayName: 'Praca', address: 'Rynek Główny 1, Kraków' })
})

test('adds a location with browser geolocation ("Find me")', async ({ page, context }) => {
  await context.grantPermissions(['geolocation'])
  await context.setGeolocation({ latitude: 52.2297, longitude: 21.0122 })

  const items: Saved[] = []
  let sent: Record<string, unknown> | null = null

  await openSavedLocations(page, items, [
    {
      method: 'POST',
      path: '/api/saved-locations',
      respond: ({ body }) => {
        sent = body
        const created = { ...home, id: 's3', displayName: String(body?.displayName), latitude: 52.2297, longitude: 21.0122 }
        items.push(created)
        return { status: 201, json: created }
      },
    },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Biuro')
  await page.getByRole('button', { name: 'Wykryj moją lokalizację' }).click()
  await expect(page.getByText('Wykryto lokalizację: 52.22970, 21.01220')).toBeVisible()
  await page.getByRole('button', { name: 'Zapisz lokalizację' }).click()

  await expect(page.getByText('Biuro', { exact: true })).toBeVisible()
  expect(sent).toEqual({ displayName: 'Biuro', latitude: 52.2297, longitude: 21.0122 })
})

test('shows a readable message when geolocation is denied', async ({ page, context }) => {
  await context.clearPermissions()
  await openSavedLocations(page, [])

  await page.getByRole('button', { name: 'Wykryj moją lokalizację' }).click()
  await expect(page.getByText(/Odmówiono dostępu do lokalizacji|Nie można określić|Nie udało się pobrać lokalizacji/)).toBeVisible()
})

test('shows the server message when the address is not found', async ({ page }) => {
  await openSavedLocations(page, [], [
    {
      method: 'POST',
      path: '/api/saved-locations',
      respond: () => ({ status: 400, json: { title: 'Bad Request', detail: 'Nie znaleziono podanego adresu.' } }),
    },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Nigdzie')
  await page.getByRole('button', { name: 'Wpisz adres' }).click()
  await page.getByLabel('Adres').fill('asdfghjkl')
  await page.getByRole('button', { name: 'Zapisz lokalizację' }).click()

  await expect(page.getByText('Nie znaleziono podanego adresu.')).toBeVisible()
})

test('shows a generic message when geocoding is unavailable', async ({ page }) => {
  await openSavedLocations(page, [], [
    { method: 'POST', path: '/api/saved-locations', respond: () => ({ status: 502, json: { title: 'Bad Gateway' } }) },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Dom')
  await page.getByRole('button', { name: 'Wpisz adres' }).click()
  await page.getByLabel('Adres').fill('Rynek 1')
  await page.getByRole('button', { name: 'Zapisz lokalizację' }).click()

  await expect(page.getByText('Nie udało się ustalić współrzędnych dla podanego adresu. Spróbuj ponownie później.')).toBeVisible()
})

test('deletes a location only after confirmation', async ({ page }) => {
  const items = [home]

  await openSavedLocations(page, items, [
    {
      method: 'DELETE',
      path: '/api/saved-locations/s1',
      respond: () => {
        items.pop()
        return { status: 204 }
      },
    },
  ])

  await page.getByRole('button', { name: 'Usuń: Dom' }).click()
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByText('Lokalizacja „Dom” zostanie usunięta.')).toBeVisible()

  // Cancel keeps the entry.
  await dialog.getByRole('button', { name: 'Anuluj' }).click()
  await expect(page.getByText('Dom', { exact: true })).toBeVisible()

  await page.getByRole('button', { name: 'Usuń: Dom' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Usuń', exact: true }).click()

  await expect(page.getByText('Nie masz jeszcze zapisanych lokalizacji.')).toBeVisible()
})
