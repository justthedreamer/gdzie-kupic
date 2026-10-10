import { test, expect } from '@playwright/test'
import { loginAs } from '../e2e/support/api-mock'

// Smoke test of the real SignalR hub (`/hubs/app`): signing in opens the WebSocket, and
// signing out closes it. Runs against the real backend like the other `*.real.spec.ts`.

test('signing in opens the hub connection and signing out closes it', async ({ page }) => {
  const opened = page.waitForEvent('websocket', ws => ws.url().includes('/hubs/app'))

  await loginAs(page, 'Buyer')

  const socket = await opened
  expect(socket.url()).toContain('access_token=')
  expect(socket.isClosed()).toBe(false)

  const closed = socket.waitForEvent('close')
  // On narrow screens the sign-out lives in the top bar menu.
  const menu = page.getByRole('button', { name: 'Menu', exact: true })
  const logout = page.getByRole('menuitem', { name: /Wyloguj/ }).or(page.getByRole('button', { name: /Wyloguj/ }))
  if (page.viewportSize()!.width < 1024) {
    await expect(async () => {
      if (!(await logout.isVisible())) await menu.click({ timeout: 2000 })
      await logout.click({ timeout: 2000 })
    }).toPass()
  } else {
    await logout.click()
  }
  await closed
})
