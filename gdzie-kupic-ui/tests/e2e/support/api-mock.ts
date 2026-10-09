import type { Page } from '@playwright/test'

const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5211'

export interface MockRequest {
  url: URL
  body: Record<string, unknown> | null
}

export interface MockResponse {
  status?: number
  json?: unknown
}

export interface MockHandler {
  method: string
  /** Exact pathname (e.g. `/api/saved-locations`) or a pattern for ids. */
  path: string | RegExp
  respond: (req: MockRequest) => MockResponse | Promise<MockResponse>
}

const CORS_HEADERS = {
  'access-control-allow-origin': '*',
  'access-control-allow-headers': '*',
  'access-control-allow-methods': '*',
}

/**
 * Mocks the backend API for a page. Only requests to the API origin are
 * intercepted (never `/_nuxt/...` module paths). Unmatched calls answer 404 so
 * a missing mock shows up as a visible failure instead of a hang.
 */
export async function mockApi(page: Page, handlers: MockHandler[]) {
  const pattern = new RegExp(`^${API_BASE.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/api/`)

  await page.route(pattern, async (route) => {
    const request = route.request()

    if (request.method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }

    const url = new URL(request.url())
    const handler = handlers.find(h =>
      h.method === request.method()
      && (typeof h.path === 'string' ? h.path === url.pathname : h.path.test(url.pathname)),
    )

    if (!handler) {
      await route.fulfill({ status: 404, headers: CORS_HEADERS, json: { title: 'No mock' } })
      return
    }

    const { status = 200, json } = await handler.respond({
      url,
      body: request.postDataJSON() as Record<string, unknown> | null,
    })

    await route.fulfill({
      status,
      headers: CORS_HEADERS,
      ...(json === undefined ? {} : { json }),
    })
  })
}

/** Clicking before Vue has hydrated is a no-op, so wait for the app first. */
export async function waitForHydration(page: Page) {
  await page.waitForFunction(() => {
    const nuxt = (window as unknown as { useNuxtApp?: () => { isHydrating: boolean } }).useNuxtApp?.()
    return nuxt !== undefined && !nuxt.isHydrating
  })
}

/**
 * Signs in through the Dev account switcher. Buyers land on `/home`, Merchants
 * on `/feed`, other roles stay on `/`. The auth state is client-only (not
 * persisted), so tests must navigate client-side afterwards — via the nav
 * links — rather than with `page.goto()`.
 */
export async function loginAs(page: Page, role: 'Admin' | 'Buyer' | 'Merchant') {
  await page.goto('/')
  await waitForHydration(page)
  await page.getByRole('button', { name: 'Dev', exact: true }).click()
  await page.getByRole('menuitem', { name: new RegExp(`^${role}`) }).click()
}

/**
 * Opens an entry of the Buyer/Merchant shell navigation: the sidebar link on
 * desktop, the header overflow menu ("Menu") on mobile. Matches the Polish label.
 */
export async function openShellNav(page: Page, label: string) {
  const isDesktop = (page.viewportSize()?.width ?? 0) >= 1024

  if (isDesktop) {
    await page.getByRole('link', { name: label }).click()
    return
  }

  await page.getByRole('button', { name: 'Menu' }).click()
  await page.getByRole('menuitem', { name: label }).click()
}
