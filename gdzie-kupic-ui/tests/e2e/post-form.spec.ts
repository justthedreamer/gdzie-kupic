import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, type MockHandler } from './support/api-mock'

// The Service ticket lands separately, so these tests run against the API contract
// (planning/phase-4-post-lifecycle-matching.md § API contract) with mocked responses.

const categories = [
  {
    id: 'c1',
    name: 'Sport',
    isDisabled: false,
    tags: [
      { id: 't1', name: 'Rowery', isDisabled: false },
      { id: 't2', name: 'Narty', isDisabled: true },
    ],
  },
  {
    id: 'c2',
    name: 'Muzyka',
    isDisabled: false,
    tags: [{ id: 't3', name: 'Mikrofony', isDisabled: false }],
  },
  { id: 'c3', name: 'Wyłączona', isDisabled: true, tags: [] },
]

const home = {
  id: 's1',
  displayName: 'Dom',
  latitude: 50.0647,
  longitude: 19.945,
  addressDisplayName: '31-042 Kraków, Polska',
  createdAt: '2026-01-01T00:00:00Z',
}

const createdPost = { id: 'p1', title: 'Rower górski', status: 'Active' }

interface Spy {
  posts: Record<string, unknown>[]
  savedLocations: Record<string, unknown>[]
}

function handlers(spy: Spy, overrides: { post?: MockHandler['respond'], saveLocation?: MockHandler['respond'] } = {}): MockHandler[] {
  return [
    { method: 'GET', path: '/api/catalogue/categories', respond: () => ({ json: categories }) },
    { method: 'GET', path: '/api/saved-locations', respond: () => ({ json: [home] }) },
    {
      method: 'GET',
      path: '/api/location/search',
      respond: () => ({ json: { latitude: 50.0617, longitude: 19.9373, formattedAddress: 'Rynek Główny 1, 31-042 Kraków, Polska' } }),
    },
    {
      method: 'POST',
      path: '/api/posts',
      respond: (req) => {
        spy.posts.push(req.body ?? {})
        return overrides.post ? overrides.post(req) : { status: 201, json: createdPost }
      },
    },
    {
      method: 'POST',
      path: '/api/saved-locations',
      respond: (req) => {
        spy.savedLocations.push(req.body ?? {})
        return overrides.saveLocation ? overrides.saveLocation(req) : { status: 201, json: { ...home, id: 's2' } }
      },
    },
  ]
}

async function openForm(page: Page, spy: Spy, overrides?: Parameters<typeof handlers>[1]) {
  await mockApi(page, handlers(spy, overrides))
  await loginAs(page, 'Buyer')
  await expect(page).toHaveURL('/home')
  // dispatchEvent: on mobile the Nuxt devtools overlay covers the bottom tab bar link.
  await page.getByRole('link', { name: 'Nowe zapytanie' }).first().dispatchEvent('click')
  await expect(page).toHaveURL('/requests/new')
  await expect(page.getByRole('heading', { name: 'Nowe zapytanie', level: 1 })).toBeVisible()
}

async function choose(page: Page, label: string, option: string) {
  await page.getByRole('combobox', { name: label }).click()
  await page.getByRole('option', { name: option }).click()
}

async function fillBasics(page: Page) {
  await page.getByLabel('Tytuł').fill('Rower górski')
  await choose(page, 'Kategoria', 'Sport')
  await choose(page, 'Tag', 'Rowery')
}

const publish = (page: Page) => page.getByRole('button', { name: 'Opublikuj zapytanie' })

function newSpy(): Spy {
  return { posts: [], savedLocations: [] }
}

test('shows validation errors and sends nothing for an empty form', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await publish(page).click()

  await expect(page.getByText('Podaj tytuł.')).toBeVisible()
  await expect(page.getByText('Wybierz kategorię.')).toBeVisible()
  await expect(page.getByText('Wybierz zapisaną lokalizację lub wskaż nową.')).toBeVisible()
  expect(spy.posts).toHaveLength(0)
})

test('creates a request with a saved location and a preset radius', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByLabel('Opis').fill('Najlepiej z amortyzatorem')
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByRole('radio', { name: '25 km' }).click()
  await publish(page).click()

  await expect(page).toHaveURL('/requests/p1')
  expect(spy.posts).toEqual([{
    latitude: 50.0647,
    longitude: 19.945,
    radiusKm: 25,
    categoryId: 'c1',
    tagId: 't1',
    title: 'Rower górski',
    description: 'Najlepiej z amortyzatorem',
  }])
  // A saved location needs no "save it" offer.
  expect(spy.savedLocations).toHaveLength(0)
})

test('unlimited radius sends a null radius', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByRole('radio', { name: 'Bez limitu' }).click()
  await publish(page).click()

  await expect(page).toHaveURL('/requests/p1')
  expect(spy.posts[0]).toMatchObject({ radiusKm: null })
})

test('a custom radius must be greater than 0 and is sent as entered', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByRole('radio', { name: 'Własny' }).click()
  await page.getByRole('textbox', { name: 'Własny' }).fill('0')
  await publish(page).click()

  await expect(page.getByText('Podaj zasięg większy od 0.')).toBeVisible()
  expect(spy.posts).toHaveLength(0)

  await page.getByRole('textbox', { name: 'Własny' }).fill('120')
  await publish(page).click()

  await expect(page).toHaveURL('/requests/p1')
  expect(spy.posts[0]).toMatchObject({ radiusKm: 120 })
})

test('offers only enabled categories and tags of the chosen category', async ({ page }) => {
  await openForm(page, newSpy())

  await page.getByRole('combobox', { name: 'Kategoria' }).click()
  await expect(page.getByRole('option', { name: 'Sport' })).toBeVisible()
  await expect(page.getByRole('option', { name: 'Muzyka' })).toBeVisible()
  await expect(page.getByRole('option', { name: 'Wyłączona' })).toHaveCount(0)
  await page.getByRole('option', { name: 'Sport' }).click()

  await page.getByRole('combobox', { name: 'Tag' }).click()
  await expect(page.getByRole('option', { name: 'Rowery' })).toBeVisible()
  await expect(page.getByRole('option', { name: 'Narty' })).toHaveCount(0)
  await expect(page.getByRole('option', { name: 'Mikrofony' })).toHaveCount(0)
})

test('urgent requests need a deadline and send it as an ISO timestamp', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByRole('switch', { name: /Pilne/ }).click()
  await publish(page).click()

  await expect(page.getByText('Wybierz termin.')).toBeVisible()
  expect(spy.posts).toHaveLength(0)

  const deadline = new Date(Date.now() + 5 * 60 * 60 * 1000)
  const local = `${deadline.getFullYear()}-${String(deadline.getMonth() + 1).padStart(2, '0')}-${String(deadline.getDate()).padStart(2, '0')}T${String(deadline.getHours()).padStart(2, '0')}:${String(deadline.getMinutes()).padStart(2, '0')}`
  await page.getByLabel('Termin').fill(local)
  await publish(page).click()

  await expect(page).toHaveURL('/requests/p1')
  const sent = String(spy.posts[0]!.urgentDeadline)
  expect(Math.abs(new Date(sent).getTime() - deadline.getTime())).toBeLessThan(60_000)
})

test('a deadline closer than one hour is rejected', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByRole('switch', { name: /Pilne/ }).click()

  const soon = new Date(Date.now() + 10 * 60 * 1000)
  const local = `${soon.getFullYear()}-${String(soon.getMonth() + 1).padStart(2, '0')}-${String(soon.getDate()).padStart(2, '0')}T${String(soon.getHours()).padStart(2, '0')}:${String(soon.getMinutes()).padStart(2, '0')}`
  await page.getByLabel('Termin').fill(local)
  await publish(page).click()

  await expect(page.getByText('Termin musi być za co najmniej 1 godz.')).toBeVisible()
  expect(spy.posts).toHaveLength(0)
})

test('a typed location is offered for saving after the request is created', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
  await page.getByRole('button', { name: 'Szukaj' }).click()
  await expect(page.getByText('Rynek Główny 1, 31-042 Kraków, Polska')).toBeVisible()
  await publish(page).click()

  const dialog = page.getByRole('dialog')
  await expect(dialog.getByText('Zapisać tę lokalizację?')).toBeVisible()
  expect(spy.posts[0]).toMatchObject({ latitude: 50.0617, longitude: 19.9373 })

  await dialog.getByLabel('Nazwa').fill('Rynek')
  await dialog.getByRole('button', { name: 'Zapisz lokalizację' }).click()

  await expect(page).toHaveURL('/requests/p1')
  expect(spy.savedLocations).toEqual([{ displayName: 'Rynek', latitude: 50.0617, longitude: 19.9373 }])
})

test('skipping the save offer still opens the new request', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
  await page.getByRole('button', { name: 'Szukaj' }).click()
  await expect(page.getByText('Rynek Główny 1, 31-042 Kraków, Polska')).toBeVisible()
  await publish(page).click()

  await page.getByRole('dialog').getByRole('button', { name: 'Pomiń' }).click()

  await expect(page).toHaveURL('/requests/p1')
  expect(spy.savedLocations).toHaveLength(0)
})

test('a failed location save does not block opening the request', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy, { saveLocation: () => ({ status: 500 }) })

  await fillBasics(page)
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
  await page.getByRole('button', { name: 'Szukaj' }).click()
  await expect(page.getByText('Rynek Główny 1, 31-042 Kraków, Polska')).toBeVisible()
  await publish(page).click()

  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Nazwa').fill('Rynek')
  await dialog.getByRole('button', { name: 'Zapisz lokalizację' }).click()
  const close = dialog.getByRole('button', { name: 'Zamknij', exact: true })
  await expect(close).toBeVisible()
  await close.click()
  await expect(page).toHaveURL('/requests/p1')
})

test('choosing a saved location and typing one are mutually exclusive', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy)

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
  await page.getByRole('button', { name: 'Szukaj' }).click()
  await expect(page.getByText('Rynek Główny 1, 31-042 Kraków, Polska')).toBeVisible()

  // The typed location replaced the saved one…
  await expect(page.getByRole('radio', { name: /Dom/ })).not.toBeChecked()

  // …and picking the saved one clears the typed result again.
  await page.getByRole('radio', { name: /Dom/ }).click()
  await expect(page.getByText('Rynek Główny 1, 31-042 Kraków, Polska')).toBeHidden()

  await publish(page).click()
  await expect(page).toHaveURL('/requests/p1')
  expect(spy.posts[0]).toMatchObject({ latitude: 50.0647, longitude: 19.945 })
  expect(spy.savedLocations).toHaveLength(0)
})

test('shows the server validation message and stays on the form', async ({ page }) => {
  const spy = newSpy()
  await openForm(page, spy, {
    post: () => ({ status: 400, json: { title: 'Bad Request', detail: 'Wybrany tag nie należy do kategorii.' } }),
  })

  await fillBasics(page)
  await page.getByRole('radio', { name: /Dom/ }).click()
  await publish(page).click()

  await expect(page.getByText('Wybrany tag nie należy do kategorii.')).toBeVisible()
  await expect(page).toHaveURL('/requests/new')
})
