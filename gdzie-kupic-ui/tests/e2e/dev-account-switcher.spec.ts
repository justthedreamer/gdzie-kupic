import { test, expect } from '@playwright/test'
import { waitForHydration } from './support/api-mock'

test('dev account switcher logs in as each test account with no logout required between switches', async ({ page, isMobile }) => {
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
