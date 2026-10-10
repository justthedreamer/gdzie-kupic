import { test, expect } from '@playwright/test'
import { loginAs } from '../e2e/support/api-mock'

// Smoke test of the real SignalR hub (`/hubs/app`): signing in opens the WebSocket, and
// signing out closes it. Runs against the real backend like the other `*.real.spec.ts`.

// The hub is delivered by Service ticket #120 (epic #119). Drop `fixme` when it is deployed.
test.fixme('signing in opens the hub connection and signing out closes it', async ({ page }) => {
  const opened = page.waitForEvent('websocket', ws => ws.url().includes('/hubs/app'))

  await loginAs(page, 'Buyer')

  const socket = await opened
  expect(socket.url()).toContain('access_token=')
  expect(socket.isClosed()).toBe(false)

  const closed = socket.waitForEvent('close')
  await page.getByRole('button', { name: /Wyloguj/ }).click()
  await closed
})
