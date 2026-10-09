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

async function fillBusinessStep(page: Page) {
  await page.getByLabel('Nazwa firmy').fill('Sklep Testowy')
  await page.getByLabel('Nazwa oddziału').fill('Oddział Rynek')
  await page.getByLabel('Telefon').fill('+48 600 100 200')
  await page.getByRole('button', { name: 'Wpisz adres' }).click()
  await page.getByLabel('Adres').fill('Rynek Główny 1, Kraków')
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
    branch: { displayName: 'Oddział Rynek', phone: '+48 600 100 200', address: 'Rynek Główny 1, Kraków' },
  })
  expect(subscriptionBodies).toEqual([
    { categoryId: 'c1' },
    { categoryId: 'c3', tagId: 't3' },
  ])
  await expect(page.getByText('Sport › Rowery')).toBeVisible()
})

test('a failed subscription can be retried without re-entering data or re-creating the merchant', async ({ page }) => {
  let onboarded = false
  let onboardingCalls = 0
  let subscriptionCalls = 0

  await mockApi(page, [
    catalogueHandler,
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

test('an onboarded merchant is not forced into onboarding and can remove subscriptions', async ({ page }) => {
  const subscriptions = [
    { id: 's1', categoryId: 'c1', tagId: null },
    { id: 's2', categoryId: 'c3', tagId: 't3' },
  ]

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
  ])

  await loginAs(page, 'Merchant')
  await expect(page).toHaveURL('/feed')
  await openShellNav(page, 'Ustawienia sklepu')

  await expect(page).toHaveURL('/merchant/subscriptions')
  await expect(page.getByText('Elektronika').first()).toBeVisible()
  await expect(page.getByText('Sport › Rowery')).toBeVisible()

  await page.getByRole('button', { name: 'Usuń subskrypcję: Sport › Rowery' }).click()
  await expect(page.getByText('Sport › Rowery')).toHaveCount(0)
})
