import { test, expect } from '@playwright/test'
import { waitForHydration } from './support/api-mock'

test('dev account switcher logs in as each test account with no logout required between switches', async ({ page }) => {
  await page.goto('/')
  await waitForHydration(page)

  await page.getByRole('button', { name: 'Dev', exact: true }).click()
  await page.getByRole('menuitem', { name: /^Buyer/ }).click()
  await expect(page).toHaveURL('/home')

  await page.getByRole('button', { name: 'Dev', exact: true }).click()
  await page.getByRole('menuitem', { name: /^Merchant/ }).click()
  await expect(page.getByRole('banner')).toContainText('merchant-test@gdziekupic.local')

  await page.getByRole('button', { name: 'Wyloguj się' }).click()
  await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
})
