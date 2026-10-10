# Testing

## Unit tests — Vitest

Files live in `tests/unit/`. The environment is `nuxt` (provided by `@nuxt/test-utils`), so all Nuxt auto-imports (`ref`, `computed`, composables, etc.) are available without explicit imports.

```bash
npm run test          # run once
npm run test:watch    # watch mode
```

### Writing a unit test

```ts
// tests/unit/stores/auth.test.ts
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())   // fresh store for each test
  })

  it('initialises unauthenticated', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
  })
})
```

### What to unit-test

- **Stores** — state transitions, computed values, actions
- **Composables** — pure logic (not HTTP calls; mock those with `vi.fn()`)
- **Utilities** — date formatters, validators, mappers

Avoid testing component rendering at the unit level — use Playwright for that.

---

## E2E tests — Playwright

Files live in `tests/e2e/`. Tests run against the dev server (auto-started by Playwright config).

```bash
npm run test:e2e                    # headless
npx playwright test --ui            # interactive UI mode
npx playwright test --headed        # visible browser
```

### Writing an e2e test

```ts
// tests/e2e/auth.spec.ts
import { test, expect } from '@playwright/test'

test('redirects unauthenticated user to login', async ({ page }) => {
  await page.goto('/requests/new')
  await expect(page).toHaveURL('/auth/login')
})

test('login flow', async ({ page }) => {
  await page.goto('/auth/login')
  await page.getByLabel('E-mail').fill('user@example.com')
  await page.getByLabel('Hasło').fill('password')
  await page.getByRole('button', { name: 'Zaloguj się' }).click()
  await expect(page).toHaveURL('/')
})
```

### Selectors — preferred order

1. `getByRole` — most resilient, tests accessibility
2. `getByLabel` — for form fields
3. `getByText` — for visible content
4. `getByTestId` — add `data-testid` attribute as last resort

### Mocking the API and logging in

Helpers live in [`tests/e2e/support/api-mock.ts`](../tests/e2e/support/api-mock.ts):

- `mockApi(page, handlers)` intercepts only requests to the API origin and answers 404 for anything unmocked, so a missing mock fails visibly.
- `waitForHydration(page)` — wait before the first click; interacting before Vue hydrates is a silent no-op.
- `loginAs(page, role)` signs in through the Dev account switcher. Buyers land on `/home`, Merchants on `/feed`, Admins on `/admin/catalogue`. A Merchant whose `GET /api/merchant/me` is unmocked (or 404) is redirected on to onboarding. Auth state is client-only (not persisted), so afterwards navigate with the nav links, never `page.goto()` to a protected page.
- `openShellNav(page, label)` opens an entry of the Buyer/Merchant shell: the sidebar link on desktop, the header `Menu` overflow on mobile. Logout follows the same split (sidebar button vs. `Menu` item).
- The Buyer home and the Merchant feed run on built-in sample data in dev (`buyerHomeMock`, `merchantFeedMock`, `chatMock`), which is what Playwright's dev server serves. Nuxt UI tabs (`role="tab"`, reka-ui) switch on `mousedown`, not `click`, in unit tests.

The Playwright config pins `locale: 'pl-PL'`, because the app detects the browser language and the tests assert Polish copy. Use `exact: true` when a name is a prefix of another control (e.g. the `Dev` button).

### Real-API e2e

`tests/e2e-real/` runs the same app against the real backend (no mocks), with the Dev accounts:

```bash
docker compose up --build -d      # service on http://localhost:5000
npm run test:e2e:real             # playwright.real.config.ts
```

- The service must verify the mock tokens: set `JWT_SECRET` to the dev value from `.env.example` (see `docs/local-dev.md` § Mock Accounts).
- The API allows only the origin `http://localhost:3000`, so the config starts its own dev server there (`reuseExistingServer: false`) with `NUXT_PUBLIC_API_BASE=http://localhost:5000` and the Buyer-home / Merchant-feed / chat mocks off. Stop any other `npm run dev` first.
- Tests create their own data (unique titles, a saved location when the Buyer has none) and pick a catalogue tag nobody subscribes to for the zero-match case. Matching runs in a Hangfire job, so allow up to a minute. `chat.real.spec.ts` drives the whole loop: the Buyer posts a request near the Dev Merchant's branch, the Merchant answers "Mam to" through the API, and both sides then talk (the Merchant's side over the API, the Buyer's in the UI).

### CI behaviour

`playwright.config.ts` sets `retries: 2` and `workers: 1` in CI (detected via `process.env.CI`). Locally it runs in parallel with no retries.
