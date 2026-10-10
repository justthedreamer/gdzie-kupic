import { test, expect, type Page } from '@playwright/test'
import { loginAs } from './support/api-mock'

// The mock Playwright config runs with `realtimeMock`: there is no SignalR connection and
// the test plays the server through `window.__realtime` (see docs/api.md § Real-time
// events). The default state is `disconnected`, so the screens keep their polling.

type Hook = {
  getState: () => string
  setState: (state: string) => void
  emit: (name: string, payload?: unknown) => void
  on: (name: string, handler: (payload: unknown) => void) => () => void
}

declare global {
  interface Window {
    __realtime?: Hook
    __received?: [string, unknown][]
  }
}

/** Records the given events (and `resync`) into `window.__received`. */
async function record(page: Page, names: string[]) {
  await page.evaluate((events) => {
    window.__received = []
    for (const name of events) window.__realtime!.on(name, payload => window.__received!.push([name, payload]))
  }, names)
}

const received = (page: Page) => page.evaluate(() => window.__received ?? [])

test.describe('Real-time mock', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'Buyer')
    await expect(page).toHaveURL('/home')
  })

  test('starts disconnected', async ({ page }) => {
    expect(await page.evaluate(() => window.__realtime!.getState())).toBe('disconnected')
  })

  test('delivers a server event with its payload to subscribers', async ({ page }) => {
    await record(page, ['postStatusChanged', 'threadUpdated'])

    await page.evaluate(() => window.__realtime!.emit('postStatusChanged', { postId: 'p1' }))

    expect(await received(page)).toEqual([['postStatusChanged', { postId: 'p1' }]])
  })

  test('becoming connected asks the subscribers to resync, dropping does not', async ({ page }) => {
    await record(page, ['resync'])

    await page.evaluate(() => window.__realtime!.setState('connected'))
    expect(await page.evaluate(() => window.__realtime!.getState())).toBe('connected')
    expect(await received(page)).toEqual([['resync', undefined]])

    await page.evaluate(() => window.__realtime!.setState('reconnecting'))
    expect(await received(page)).toHaveLength(1)
  })

  test('an unsubscribed handler gets nothing', async ({ page }) => {
    await page.evaluate(() => {
      window.__received = []
      const off = window.__realtime!.on('postAdded', payload => window.__received!.push(['postAdded', payload]))
      off()
      window.__realtime!.emit('postAdded', { postId: 'p1' })
    })

    expect(await received(page)).toEqual([])
  })
})
