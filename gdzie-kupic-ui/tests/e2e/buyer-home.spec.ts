import { test, expect } from '@playwright/test'
import { loginAs } from './support/api-mock'

// The Buyer home runs on mocked data in dev (`buyerHomeMock`), which is what
// the dev server used by Playwright serves.

test.describe('Buyer home', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
  })

  test('is the default page after login and shows the main widgets', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Aktywne zapytania' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Szukam mikrofonu Shure SM7B', level: 2 })).toBeVisible()
    await expect(page.getByTestId('notified-count')).toHaveText('14')
    await expect(page.getByRole('heading', { name: 'Ostatnia aktywność' })).toBeVisible()
  })

  test('selecting another request updates the summary and live status', async ({ page }) => {
    await page.getByRole('button', { name: /Szukam używanego iPhone 14 Pro/ }).click()

    await expect(page.getByRole('heading', { name: 'Szukam używanego iPhone 14 Pro', level: 2 })).toBeVisible()
    await expect(page.getByTestId('notified-count')).toHaveText('22')
    await expect(page.getByText('iStore Wrocław').first()).toBeVisible()
  })

  test('the logo leads back to /home', async ({ page, isMobile }) => {
    test.skip(isMobile, 'the logo lives in the desktop sidebar')

    await page.getByRole('link', { name: 'GdzieKupić' }).first().click()
    await expect(page).toHaveURL('/home')
  })

  test('recent chats are shown on desktop only', async ({ page, isMobile }) => {
    const chats = page.getByRole('heading', { name: 'Ostatnie czaty' })

    if (isMobile) {
      await expect(chats).toBeHidden()
    }
    else {
      await expect(chats).toBeVisible()
    }
  })

  test('desktop shows the sidebar, mobile shows bottom tabs with the new-request button', async ({ page, isMobile }) => {
    if (isMobile) {
      const tabs = page.getByRole('navigation', { name: 'Nawigacja' })
      await expect(tabs).toBeVisible()
      await expect(tabs.getByRole('link', { name: 'Nowe zapytanie' })).toHaveAttribute('href', '/requests/new')
      await expect(page.getByRole('navigation', { name: 'Nawigacja główna' })).toBeHidden()
    }
    else {
      const sidebar = page.getByRole('navigation', { name: 'Nawigacja główna' })
      await expect(sidebar.getByRole('link', { name: 'Pulpit' })).toBeVisible()
      await expect(sidebar.getByRole('button', { name: /Czaty/ })).toBeDisabled()
    }
  })
})
