import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi, type MockHandler } from './support/api-mock'

function account(role: 'Buyer' | 'Merchant', firstName: string | null) {
  return { email: `${role.toLowerCase()}@gdziekupic.local`, firstName, role }
}

/** A fake account endpoint that stores what the page saves; `puts` records the bodies. */
function accountHandlers(role: 'Buyer' | 'Merchant', initial: string | null, puts: unknown[] = []): MockHandler[] {
  let firstName = initial

  return [
    { method: 'GET', path: '/api/account/profile', respond: () => ({ json: account(role, firstName) }) },
    {
      method: 'PUT',
      path: '/api/account/profile',
      respond: ({ body }) => {
        puts.push(body)
        firstName = (body?.firstName as string | null) ?? null
        return { json: account(role, firstName) }
      },
    },
  ]
}

async function openAccount(page: Page, role: 'Buyer' | 'Merchant', handlers: MockHandler[]) {
  await mockApi(page, handlers)
  await loginAs(page, role)
  // A sidebar link on desktop, a bottom tab on mobile.
  await page.getByRole('link', { name: 'Profil' }).click()
  await expect(page).toHaveURL('/account')
}

const firstNameInput = (page: Page) => page.getByTestId('account-first-name')
const saveButton = (page: Page) => page.getByRole('button', { name: 'Zapisz' })

test('a buyer sets a first name from the profile page', async ({ page }) => {
  const puts: unknown[] = []
  await openAccount(page, 'Buyer', accountHandlers('Buyer', null, puts))

  await expect(page.getByTestId('account-email')).toHaveValue('buyer-test@gdziekupic.local')
  await expect(firstNameInput(page)).toHaveValue('')
  await expect(saveButton(page)).toBeDisabled()

  await firstNameInput(page).fill('  Anna  ')
  await saveButton(page).click()

  await expect(page.getByTestId('account-saved')).toBeVisible()
  await expect(firstNameInput(page)).toHaveValue('Anna')
  expect(puts).toEqual([{ firstName: 'Anna' }])
})

test('shows the stored first name and can clear it', async ({ page }) => {
  const puts: unknown[] = []
  await openAccount(page, 'Buyer', accountHandlers('Buyer', 'Anna', puts))

  await expect(firstNameInput(page)).toHaveValue('Anna')

  await firstNameInput(page).fill('')
  await saveButton(page).click()

  await expect(page.getByTestId('account-saved')).toBeVisible()
  expect(puts).toEqual([{ firstName: null }])
})

test('rejects an invalid first name before calling the server', async ({ page }) => {
  const puts: unknown[] = []
  await openAccount(page, 'Buyer', accountHandlers('Buyer', null, puts))

  await firstNameInput(page).fill('Anna1')

  await expect(page.getByText('Imię może zawierać tylko litery, spacje, myślniki i apostrofy.')).toBeVisible()
  await expect(saveButton(page)).toBeDisabled()
  expect(puts).toEqual([])
})

test('a merchant can open the profile page too', async ({ page }) => {
  const merchant = {
    merchantId: 'm1',
    name: 'Sklep Testowy',
    description: null,
    branch: { id: 'b1', displayName: 'Oddział Rynek', latitude: 50.06, longitude: 19.94, addressDisplayName: null, phone: null, website: null },
  }

  await openAccount(page, 'Merchant', [
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: merchant }) },
    ...accountHandlers('Merchant', 'Piotr'),
  ])

  await expect(firstNameInput(page)).toHaveValue('Piotr')
})

test('shows an inline error when saving fails', async ({ page }) => {
  await openAccount(page, 'Buyer', [
    { method: 'GET', path: '/api/account/profile', respond: () => ({ json: account('Buyer', null) }) },
    { method: 'PUT', path: '/api/account/profile', respond: () => ({ status: 500, json: { title: 'boom' } }) },
  ])

  await firstNameInput(page).fill('Anna')
  await saveButton(page).click()

  await expect(page.getByTestId('account-save-error')).toBeVisible()
  await expect(page.getByTestId('account-saved')).toBeHidden()
})
