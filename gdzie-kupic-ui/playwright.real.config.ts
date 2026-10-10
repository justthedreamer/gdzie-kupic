import { defineConfig, devices } from '@playwright/test'

// E2E against the real backend. The API only allows the origin http://localhost:3000 (CORS),
// so the dev server must own that port: stop any other `npm run dev` first.
const API_BASE = process.env.NUXT_PUBLIC_API_BASE ?? 'http://localhost:5000'

export default defineConfig({
  testDir: './tests/e2e-real',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',

  use: {
    baseURL: 'http://localhost:3000',
    locale: 'pl-PL',
    trace: 'on-first-retry',
  },

  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'Mobile Chrome', use: { ...devices['Pixel 5'] } },
  ],

  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: false,
    env: {
      NUXT_PUBLIC_API_BASE: API_BASE,
      NUXT_PUBLIC_BUYER_HOME_MOCK: 'false',
      NUXT_PUBLIC_MERCHANT_FEED_MOCK: 'false',
      NUXT_PUBLIC_CHAT_MOCK: 'false',
    },
  },
})
