import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, openShellNav, type MockHandler } from './support/api-mock'

interface Saved {
  id: string
  displayName: string
  latitude: number
  longitude: number
  addressDisplayName: string | null
  createdAt: string
}

const home: Saved = {
  id: 's1',
  displayName: 'Dom',
  latitude: 50.0647,
  longitude: 19.945,
  addressDisplayName: '31-042 Kraków, Polska',
  createdAt: '2026-01-01T00:00:00Z',
}

async function openSavedLocations(page: Page, items: Saved[], extra: MockHandler[] = []) {
  await mockApi(page, [
    { method: 'GET', path: '/api/saved-locations', respond: () => ({ json: items }) },
    ...extra,
  ])
  await loginAs(page, 'Buyer')
  await openShellNav(page, 'Moje lokalizacje')
  await expect(page).toHaveURL('/saved-locations')
}

function createHandler(items: Saved[], onBody: (body: Record<string, unknown> | null) => void, id = 's2'): MockHandler {
  return {
    method: 'POST',
    path: '/api/saved-locations',
    respond: ({ body }) => {
      onBody(body)
      const created = {
        ...home,
        id,
        displayName: String(body?.displayName),
        latitude: Number(body?.latitude),
        longitude: Number(body?.longitude),
        addressDisplayName: null,
      }
      items.push(created)
      return { status: 201, json: created }
    },
  }
}

async function fillRequiredAddress(page: Page) {
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
}

const searchButton = (page: Page) => page.getByRole('button', { name: 'Szukaj' })
const saveButton = (page: Page) => page.getByRole('button', { name: 'Zapisz lokalizację' })

test('shows the empty state and the saved list', async ({ page }) => {
  await openSavedLocations(page, [])
  await expect(page.getByText('Nie masz jeszcze zapisanych lokalizacji.')).toBeVisible()
})

test('lists saved locations with a readable address', async ({ page }) => {
  await openSavedLocations(page, [home])
  await expect(page.getByText('Dom', { exact: true })).toBeVisible()
  await expect(page.getByText('31-042 Kraków, Polska')).toBeVisible()
  await expect(page.getByText('50.06470, 19.94500')).toBeHidden()
})

test('falls back to coordinates for locations without a stored address', async ({ page }) => {
  await openSavedLocations(page, [{ ...home, addressDisplayName: null }])
  await expect(page.getByText('50.06470, 19.94500')).toBeVisible()
})

test('the address fields are always visible and Search and Save start disabled', async ({ page }) => {
  await openSavedLocations(page, [])

  await expect(page.getByLabel('Kod pocztowy')).toBeVisible()
  await expect(page.getByLabel('Miasto')).toBeVisible()
  await expect(page.getByLabel('Ulica')).toBeVisible()
  await expect(page.getByLabel('Numer domu')).toBeVisible()
  await expect(searchButton(page)).toBeDisabled()
  await expect(saveButton(page)).toBeDisabled()
})

test('Search needs a valid postal code and a city; street and house number are optional', async ({ page }) => {
  await openSavedLocations(page, [])

  await page.getByLabel('Kod pocztowy').fill('310')
  await page.getByLabel('Miasto').fill('Kraków')
  await expect(page.getByText('Podaj kod pocztowy w formacie 00-000.')).toBeVisible()
  await expect(searchButton(page)).toBeDisabled()

  await page.getByLabel('Kod pocztowy').fill('31-042')
  await expect(searchButton(page)).toBeEnabled()
})

test('the postal code adds the dash itself while typing', async ({ page }) => {
  await openSavedLocations(page, [])
  const postalCode = page.getByLabel('Kod pocztowy')

  await postalCode.pressSequentially('31')
  await expect(postalCode).toHaveValue('31-')

  await postalCode.pressSequentially('042')
  await expect(postalCode).toHaveValue('31-042')

  // Backspace removes the dash and does not bring it back.
  await postalCode.fill('')
  await postalCode.pressSequentially('31')
  await postalCode.press('Backspace')
  await expect(postalCode).toHaveValue('31')

  await postalCode.fill('')
  await postalCode.pressSequentially('abc31042')
  await expect(postalCode).toHaveValue('31-042')
})

test('adds a location found by address', async ({ page }) => {
  const items: Saved[] = []
  let sent: Record<string, unknown> | null = null
  const searched: string[] = []

  await openSavedLocations(page, items, [
    {
      method: 'GET',
      path: '/api/location/search',
      respond: ({ url }) => {
        searched.push(url.searchParams.get('address') ?? '')
        return { json: { latitude: 50.0617, longitude: 19.9373, formattedAddress: 'Rynek Główny 1, 31-042 Kraków, Polska' } }
      },
    },
    createHandler(items, (body) => { sent = body }),
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Praca')
  await fillRequiredAddress(page)
  await page.getByLabel('Ulica').fill('Rynek Główny')
  await page.getByLabel('Numer domu').fill('1')

  // Nothing to save until the address has been searched.
  await expect(saveButton(page)).toBeDisabled()

  await searchButton(page).click()
  await expect(page.getByText('Znaleziono adres: Rynek Główny 1, 31-042 Kraków, Polska')).toBeVisible()
  await expect(saveButton(page)).toBeEnabled()

  await saveButton(page).click()

  await expect(page.getByText('Praca', { exact: true })).toBeVisible()
  expect(searched).toEqual(['Rynek Główny 1, 31-042 Kraków, Polska'])
  expect(sent).toEqual({ displayName: 'Praca', latitude: 50.0617, longitude: 19.9373 })
})

test('searches with only the required fields', async ({ page }) => {
  const searched: string[] = []

  await openSavedLocations(page, [], [
    {
      method: 'GET',
      path: '/api/location/search',
      respond: ({ url }) => {
        searched.push(url.searchParams.get('address') ?? '')
        return { json: { latitude: 50.06, longitude: 19.94, formattedAddress: '31-042 Kraków, Polska' } }
      },
    },
  ])

  await fillRequiredAddress(page)
  await page.getByLabel('Miasto').press('Enter')

  await expect(page.getByText('Znaleziono adres: 31-042 Kraków, Polska')).toBeVisible()
  expect(searched).toEqual(['31-042 Kraków, Polska'])
})

test('changing the address after a search disables Save again', async ({ page }) => {
  await openSavedLocations(page, [], [
    {
      method: 'GET',
      path: '/api/location/search',
      respond: () => ({ json: { latitude: 50.06, longitude: 19.94, formattedAddress: '31-042 Kraków, Polska' } }),
    },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Praca')
  await fillRequiredAddress(page)
  await searchButton(page).click()
  await expect(saveButton(page)).toBeEnabled()

  await page.getByLabel('Miasto').fill('Warszawa')
  await expect(saveButton(page)).toBeDisabled()
  await expect(page.getByText(/Znaleziono adres/)).toBeHidden()
})

test('shows a message when the address is not found', async ({ page }) => {
  await openSavedLocations(page, [], [
    { method: 'GET', path: '/api/location/search', respond: () => ({ status: 404, json: { title: 'Not found' } }) },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Nigdzie')
  await fillRequiredAddress(page)
  await searchButton(page).click()

  await expect(page.getByText('Nie znaleziono podanego adresu. Sprawdź dane i spróbuj ponownie.')).toBeVisible()
  await expect(saveButton(page)).toBeDisabled()
})

test('shows a generic message when address search is unavailable', async ({ page }) => {
  await openSavedLocations(page, [], [
    { method: 'GET', path: '/api/location/search', respond: () => ({ status: 502, json: { title: 'Bad Gateway' } }) },
  ])

  await fillRequiredAddress(page)
  await searchButton(page).click()

  await expect(page.getByText('Nie udało się wyszukać adresu. Spróbuj ponownie później lub użyj wykrywania automatycznego.')).toBeVisible()
})

test('detects the location after the user agrees', async ({ page, context }) => {
  await context.grantPermissions(['geolocation'])
  await context.setGeolocation({ latitude: 52.2297, longitude: 21.0122 })

  const items: Saved[] = []
  let sent: Record<string, unknown> | null = null

  await openSavedLocations(page, items, [
    {
      method: 'GET',
      path: '/api/location',
      respond: () => ({ json: { voivodeship: 'Mazowieckie', postalCode: '00-001', city: 'Warszawa', country: 'Polska' } }),
    },
    createHandler(items, (body) => { sent = body }, 's3'),
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Biuro')
  await page.getByRole('button', { name: 'Wykryj automatycznie' }).click()

  // The location is only requested after the explicit consent.
  const dialog = page.getByRole('dialog')
  await expect(dialog.getByText('Użyć Twojej lokalizacji?')).toBeVisible()
  await expect(page.getByText(/Wykryto lokalizację/)).toBeHidden()
  await dialog.getByRole('button', { name: 'Zezwalam' }).click()

  await expect(page.getByText('Wykryto lokalizację: 00-001 Warszawa, Polska')).toBeVisible()
  await saveButton(page).click()

  await expect(page.getByText('Biuro', { exact: true })).toBeVisible()
  expect(sent).toEqual({ displayName: 'Biuro', latitude: 52.2297, longitude: 21.0122 })
})

test('keeps the coordinates when the detected address cannot be resolved', async ({ page, context }) => {
  await context.grantPermissions(['geolocation'])
  await context.setGeolocation({ latitude: 52.2297, longitude: 21.0122 })

  await openSavedLocations(page, [], [
    { method: 'GET', path: '/api/location', respond: () => ({ status: 502, json: { title: 'Bad Gateway' } }) },
  ])

  await page.getByLabel('Nazwa', { exact: true }).fill('Biuro')
  await page.getByRole('button', { name: 'Wykryj automatycznie' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Zezwalam' }).click()

  await expect(page.getByText('Wykryto lokalizację: 52.22970, 21.01220')).toBeVisible()
  await expect(saveButton(page)).toBeEnabled()
})

test('declining the consent does not detect the location', async ({ page, context }) => {
  await context.grantPermissions(['geolocation'])
  await context.setGeolocation({ latitude: 52.2297, longitude: 21.0122 })
  await openSavedLocations(page, [])

  await page.getByRole('button', { name: 'Wykryj automatycznie' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Anuluj' }).click()

  await expect(page.getByRole('dialog')).toBeHidden()
  await expect(page.getByText(/Wykryto lokalizację/)).toBeHidden()
  await expect(saveButton(page)).toBeDisabled()
})

test('shows a readable message when geolocation is denied', async ({ page, context }) => {
  await context.clearPermissions()
  await openSavedLocations(page, [])

  await page.getByRole('button', { name: 'Wykryj automatycznie' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Zezwalam' }).click()

  await expect(page.getByText(/Odmówiono dostępu do lokalizacji|Nie można określić|Nie udało się pobrać lokalizacji/)).toBeVisible()
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
