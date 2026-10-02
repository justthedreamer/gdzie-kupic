import { test, expect } from '@playwright/test'

test('redirects unauthenticated user to login', async ({ page }) => {
  await page.goto('/requests/new')
  await expect(page).toHaveURL('/auth/login')
})
