import { test, expect } from '@playwright/test'

test('dev account switcher logs in as each test account with no logout required between switches', async ({ page }) => {
  await page.goto('/')

  await page.getByRole('button', { name: 'Dev' }).click()
  await page.getByRole('menuitem', { name: /^Buyer/ }).click()
  await expect(page.getByText('buyer-test@gdziekupic.local')).toBeVisible()

  await page.getByRole('button', { name: 'Dev' }).click()
  await page.getByRole('menuitem', { name: /^Merchant/ }).click()
  await expect(page.getByText('merchant-test@gdziekupic.local')).toBeVisible()

  await page.getByRole('button', { name: 'Wyloguj się' }).click()
  await expect(page.getByRole('link', { name: 'Zaloguj się' })).toBeVisible()
})
