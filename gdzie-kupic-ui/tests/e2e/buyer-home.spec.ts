import { test, expect } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'
import { hoursFromNow, postPath, sampleStatus, samplePost } from './support/posts'

// Requests and their status come from the posts API (mocked here); merchant activity
// stays mocked by the app in dev (`buyerHomeMock`), and the recent chats come from the chat mock
// (`chatMock`), which is what the dev server serves.

const microphone = samplePost({ id: 'req-1', title: 'Szukam mikrofonu Shure SM7B', notifiedCount: 14, createdAt: hoursFromNow(-0.2) })
const iphone = samplePost({ id: 'req-3', title: 'Szukam używanego iPhone 14 Pro', notifiedCount: 22, createdAt: hoursFromNow(-3) })
const notified: Record<string, number> = { 'req-1': 14, 'req-3': 22 }

test.describe('Buyer home', () => {
  test.beforeEach(async ({ page }) => {
    await mockApi(page, [
      { method: 'GET', path: '/api/posts', respond: () => ({ json: [microphone, iphone] }) },
      {
        method: 'GET',
        path: postPath('/status'),
        respond: req => ({ json: sampleStatus({ notifiedCount: notified[req.url.pathname.split('/')[3] ?? ''] ?? 0 }) }),
      },
    ])
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
      await expect(sidebar.getByRole('link', { name: /Czaty/ })).toHaveAttribute('href', '/chat')
    }
  })

  test('the shell navigation stays on every Buyer page', async ({ page, isMobile }) => {
    const nav = page.getByRole('navigation', { name: isMobile ? 'Nawigacja' : 'Nawigacja główna' })

    // Client-side navigation only: a full reload would drop the in-memory session.
    await nav.getByRole('link', { name: 'Zapytania' }).click()
    await expect(page).toHaveURL('/requests')
    await expect(nav).toBeVisible()

    await page.getByRole('link', { name: 'Nowe zapytanie' }).first().click()
    await expect(page).toHaveURL('/requests/new')
    await expect(nav).toBeVisible()

    await nav.getByRole('link', { name: 'Pulpit' }).click()
    await expect(page).toHaveURL('/home')
    await expect(nav).toBeVisible()
  })
})
