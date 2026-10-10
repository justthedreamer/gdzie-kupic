import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, openShellNav, type MockHandler } from './support/api-mock'

const categories = [
  {
    id: 'c1',
    name: 'Elektronika',
    isDisabled: false,
    tags: [
      { id: 't1', name: 'Smartfony', isDisabled: false },
      { id: 't2', name: 'Telewizory', isDisabled: true },
    ],
  },
  { id: 'c2', name: 'Moda', isDisabled: true, tags: [] },
  { id: 'c3', name: 'Sport', isDisabled: false, tags: [{ id: 't3', name: 'Rowery', isDisabled: false }] },
]

const profile = {
  merchantId: 'm1',
  name: 'Sklep Testowy',
  description: null,
  branch: {
    id: 'b1',
    displayName: 'Oddział Rynek',
    latitude: 50.06,
    longitude: 19.94,
    addressDisplayName: 'Rynek Główny 1, Kraków',
    phone: null,
    website: null,
  },
}

const catalogueHandler: MockHandler = {
  method: 'GET',
  path: '/api/catalogue/categories',
  respond: () => ({ json: categories }),
}

const searchHandler: MockHandler = {
  method: 'GET',
  path: '/api/location/search',
  respond: () => ({ json: { latitude: 50.0617, longitude: 19.9373, formattedAddress: 'Rynek Główny 1, 31-042 Kraków, Polska' } }),
}

async function fillBusinessStep(page: Page) {
  await page.getByLabel('Nazwa firmy').fill('Sklep Testowy')
  await page.getByLabel('Nazwa oddziału').fill('Oddział Rynek')
  await page.getByLabel('Telefon').fill('+48 600 100 200')
  await page.getByLabel('Kod pocztowy').fill('31-042')
  await page.getByLabel('Miasto').fill('Kraków')
  await page.getByLabel('Ulica').fill('Rynek Główny')
  await page.getByLabel('Numer domu').fill('1')
  await page.getByRole('button', { name: 'Szukaj' }).click()
  await expect(page.getByText(/Znaleziono adres/)).toBeVisible()
  await page.getByRole('button', { name: 'Dalej' }).click()
}

test('a merchant who has not onboarded is directed into the onboarding flow', async ({ page }) => {
  await mockApi(page, [
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ status: 404, json: { title: 'Not Found' } }) },
  ])

  await loginAs(page, 'Merchant')

  await expect(page).toHaveURL('/merchant/onboarding')
  await expect(page.getByRole('heading', { name: 'Skonfiguruj swoją firmę' })).toBeVisible()
})

test('onboarding happy path: business, location, subscriptions', async ({ page }) => {
  let onboarded = false
  let onboardingBody: Record<string, unknown> | null = null
  const subscriptionBodies: Array<Record<string, unknown> | null> = []
  const subscriptions: Array<{ id: string, categoryId: string, tagId: string | null }> = []

  await mockApi(page, [
    catalogueHandler,
    searchHandler,
    {
      method: 'GET',
      path: '/api/merchant/me',
      respond: () => (onboarded ? { json: profile } : { status: 404, json: { title: 'Not Found' } }),
    },
    {
      method: 'POST',
      path: '/api/merchant/onboarding',
      respond: ({ body }) => {
        onboardingBody = body
        onboarded = true
        return { status: 201, json: profile }
      },
    },
    {
      method: 'POST',
      path: '/api/merchant/subscriptions',
      respond: ({ body }) => {
        subscriptionBodies.push(body)
        const created = { id: `s${subscriptions.length + 1}`, categoryId: String(body?.categoryId), tagId: (body?.tagId as string | undefined) ?? null }
        subscriptions.push(created)
        return { status: 201, json: created }
      },
    },
    { method: 'GET', path: '/api/merchant/subscriptions', respond: () => ({ json: subscriptions }) },
  ])

  await loginAs(page, 'Merchant')
  await expect(page).toHaveURL('/merchant/onboarding')

  await fillBusinessStep(page)
  await expect(page.getByTestId('onboarding-step')).toContainText('Krok 2 z 2')

  // Disabled categories/tags cannot be selected.
  await expect(page.getByRole('checkbox', { name: /^Moda/ })).toBeDisabled()
  await expect(page.getByRole('checkbox', { name: /^Telewizory/ })).toBeDisabled()

  await page.getByRole('checkbox', { name: /^Elektronika/ }).click()
  await page.getByRole('checkbox', { name: /^Rowery/ }).click()
  await page.getByRole('button', { name: 'Zakończ konfigurację' }).click()

  await expect(page).toHaveURL('/merchant/subscriptions')
  await expect(page.getByRole('heading', { name: 'Sklep Testowy' })).toBeVisible()

  expect(onboardingBody).toEqual({
    name: 'Sklep Testowy',
    branch: { displayName: 'Oddział Rynek', phone: '+48 600 100 200', latitude: 50.0617, longitude: 19.9373 },
  })
  expect(subscriptionBodies).toEqual([
    { categoryId: 'c1' },
    { categoryId: 'c3', tagId: 't3' },
  ])
  await expect(page.getByRole('switch', { name: 'Rowery' })).toHaveAttribute('aria-checked', 'true')
  await expect(page.getByRole('switch', { name: 'Cała kategoria: Elektronika' })).toHaveAttribute('aria-checked', 'true')
  await expect(page.getByRole('switch', { name: 'Cała kategoria: Sport' })).toHaveAttribute('aria-checked', 'false')
})

test('a failed subscription can be retried without re-entering data or re-creating the merchant', async ({ page }) => {
  let onboarded = false
  let onboardingCalls = 0
  let subscriptionCalls = 0

  await mockApi(page, [
    catalogueHandler,
    searchHandler,
    {
      method: 'GET',
      path: '/api/merchant/me',
      respond: () => (onboarded ? { json: profile } : { status: 404, json: { title: 'Not Found' } }),
    },
    {
      method: 'POST',
      path: '/api/merchant/onboarding',
      respond: () => {
        onboardingCalls++
        onboarded = true
        return { status: 201, json: profile }
      },
    },
    {
      method: 'POST',
      path: '/api/merchant/subscriptions',
      respond: ({ body }) => {
        subscriptionCalls++
        if (subscriptionCalls === 1) return { status: 500, json: { title: 'Server Error' } }
        return { status: 201, json: { id: 's1', categoryId: String(body?.categoryId), tagId: null } }
      },
    },
    { method: 'GET', path: '/api/merchant/subscriptions', respond: () => ({ json: [] }) },
  ])

  await loginAs(page, 'Merchant')
  await fillBusinessStep(page)

  await page.getByRole('checkbox', { name: /^Elektronika/ }).click()
  await page.getByRole('button', { name: 'Zakończ konfigurację' }).click()

  await expect(page.getByText(/nie wszystkie subskrypcje się powiodły/)).toBeVisible()
  await expect(page).toHaveURL('/merchant/onboarding')

  await page.getByRole('button', { name: 'Spróbuj ponownie' }).click()
  await expect(page).toHaveURL('/merchant/subscriptions')

  expect(onboardingCalls).toBe(1)
  expect(subscriptionCalls).toBe(2)
})

test('an onboarded merchant is not forced into onboarding and can toggle a tag subscription off and on', async ({ page }) => {
  const subscriptions = [
    { id: 's1', categoryId: 'c1', tagId: null },
    { id: 's2', categoryId: 'c3', tagId: 't3' },
  ]
  const created: Array<Record<string, unknown> | null> = []

  await mockApi(page, [
    catalogueHandler,
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: profile }) },
    { method: 'GET', path: '/api/merchant/subscriptions', respond: () => ({ json: subscriptions }) },
    {
      method: 'DELETE',
      path: '/api/merchant/subscriptions/s2',
      respond: () => {
        subscriptions.pop()
        return { status: 204 }
      },
    },
    {
      method: 'POST',
      path: '/api/merchant/subscriptions',
      respond: ({ body }) => {
        created.push(body)
        const sub = { id: 's9', categoryId: String(body?.categoryId), tagId: (body?.tagId as string | undefined) ?? null }
        subscriptions.push(sub)
        return { status: 201, json: sub }
      },
    },
  ])

  await loginAs(page, 'Merchant')
  await expect(page).toHaveURL('/feed')
  await openShellNav(page, 'Ustawienia sklepu')

  await expect(page).toHaveURL('/merchant/subscriptions')
  const rowery = page.getByRole('switch', { name: 'Rowery' })
  await expect(rowery).toHaveAttribute('aria-checked', 'true')

  // Tags of a subscribed category show as on.
  await expect(page.getByRole('switch', { name: 'Smartfony' })).toHaveAttribute('aria-checked', 'true')
  // Disabled catalogue items that are not subscribed cannot be switched on.
  await expect(page.getByRole('switch', { name: /^Cała kategoria: Moda/ })).toBeDisabled()

  await rowery.click()
  await expect(rowery).toHaveAttribute('aria-checked', 'false')

  await rowery.click()
  await expect(rowery).toHaveAttribute('aria-checked', 'true')
  expect(created).toEqual([{ categoryId: 'c3', tagId: 't3' }])
})

test('subscribing to a whole category drops its now-redundant tag subscriptions', async ({ page }) => {
  const subscriptions = [{ id: 's2', categoryId: 'c3', tagId: 't3' }]

  await mockApi(page, [
    catalogueHandler,
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: profile }) },
    { method: 'GET', path: '/api/merchant/subscriptions', respond: () => ({ json: subscriptions }) },
    {
      method: 'POST',
      path: '/api/merchant/subscriptions',
      respond: ({ body }) => {
        const sub = { id: 's9', categoryId: String(body?.categoryId), tagId: null }
        subscriptions.push(sub)
        return { status: 201, json: sub }
      },
    },
    {
      method: 'DELETE',
      path: '/api/merchant/subscriptions/s2',
      respond: () => {
        subscriptions.splice(0, 1)
        return { status: 204 }
      },
    },
  ])

  await loginAs(page, 'Merchant')
  await openShellNav(page, 'Ustawienia sklepu')

  await page.getByRole('switch', { name: 'Cała kategoria: Sport' }).click()

  await expect(page.getByRole('switch', { name: 'Cała kategoria: Sport' })).toHaveAttribute('aria-checked', 'true')
  expect(subscriptions).toEqual([{ id: 's9', categoryId: 'c3', tagId: null }])
})

test('switching one tag off under a whole-category subscription keeps the other active tags', async ({ page }) => {
  const subscriptions = [{ id: 's1', categoryId: 'c10', tagId: null as string | null }]
  const created: Array<Record<string, unknown> | null> = []

  await mockApi(page, [
    {
      method: 'GET',
      path: '/api/catalogue/categories',
      respond: () => ({
        json: [{
          id: 'c10',
          name: 'Dom',
          isDisabled: false,
          tags: [
            { id: 't10', name: 'Młotki', isDisabled: false },
            { id: 't11', name: 'Wiertarki', isDisabled: false },
            { id: 't12', name: 'Stare piły', isDisabled: true },
          ],
        }],
      }),
    },
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: profile }) },
    { method: 'GET', path: '/api/merchant/subscriptions', respond: () => ({ json: subscriptions }) },
    {
      method: 'POST',
      path: '/api/merchant/subscriptions',
      respond: ({ body }) => {
        created.push(body)
        const sub = { id: `n${created.length}`, categoryId: String(body?.categoryId), tagId: (body?.tagId as string | undefined) ?? null }
        subscriptions.push(sub)
        return { status: 201, json: sub }
      },
    },
    {
      method: 'DELETE',
      path: '/api/merchant/subscriptions/s1',
      respond: () => {
        subscriptions.splice(subscriptions.findIndex(s => s.id === 's1'), 1)
        return { status: 204 }
      },
    },
  ])

  await loginAs(page, 'Merchant')
  await openShellNav(page, 'Ustawienia sklepu')

  const wiertarki = page.getByRole('switch', { name: 'Wiertarki' })
  await expect(wiertarki).toHaveAttribute('aria-checked', 'true')
  await expect(wiertarki).toBeEnabled()

  await wiertarki.click()

  await expect(wiertarki).toHaveAttribute('aria-checked', 'false')
  await expect(page.getByRole('switch', { name: 'Młotki' })).toHaveAttribute('aria-checked', 'true')
  await expect(page.getByRole('switch', { name: 'Cała kategoria: Dom' })).toHaveAttribute('aria-checked', 'false')
  // Disabled tags are not carried over.
  expect(created).toEqual([{ categoryId: 'c10', tagId: 't10' }])
  expect(subscriptions).toEqual([{ id: 'n1', categoryId: 'c10', tagId: 't10' }])
})