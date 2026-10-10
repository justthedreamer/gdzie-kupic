import { test, expect } from '@playwright/test'
import { mockApi, waitForHydration } from './support/api-mock'

// The Merchant guard asks for the merchant profile before opening the feed; without
// a mock that call goes to an API nobody serves and the navigation can stall.
const merchant = {
  merchantId: 'm1',
  name: 'Sklep Testowy',
  description: null,
  branch: { id: 'b1', displayName: 'Oddział Rynek', latitude: 50.06, longitude: 19.94, addressDisplayName: null, phone: null, website: null },
}

test('dev account switcher logs in as each test account with no logout required between switches', async ({ page, isMobile }) => {
  await mockApi(page, [
    { method: 'GET', path: '/api/merchant/me', respond: () => ({ json: merchant }) },
  ])
  await page.goto('/')
  await waitForHydration(page)

  await page.getByRole('button', { name: 'Dev', exact: true }).click()
  await page.getByRole('menuitem', { name: /^Buyer/ }).click()
  await expect(page).toHaveURL('/home')

  await page.getByRole('button', { name: 'Dev', exact: true }).click()
  await page.getByRole('menuitem', { name: /^Merchant/ }).click()
  await expect(page).toHaveURL('/feed')

  // Logout lives in the sidebar on desktop and in the header menu on mobile.
  if (isMobile) {
    await page.getByRole('button', { name: 'Menu' }).click()
    await page.getByRole('menuitem', { name: 'Wyloguj się' }).click()
  }
  else {
    await page.getByRole('button', { name: 'Wyloguj się' }).click()
  }
  await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
})
