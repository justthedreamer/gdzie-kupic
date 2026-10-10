import { defineConfig, devices } from '@playwright/test'

// Mock e2e: the app runs on built-in sample data and the tests fake the API
// (tests/e2e/support/api-mock.ts). It owns port 3100 so it never reuses a dev
// server that talks to the real service on 3000.
const PORT = 3100

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${PORT}`,
    // The app defaults to Polish, but i18n also detects the browser language;
    // pin it so tests do not depend on the machine's locale.
    locale: 'pl-PL',
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'Mobile Chrome',
      use: { ...devices['Pixel 5'] },
    },
  ],

  webServer: {
    command: `npm run dev -- --port ${PORT}`,
    url: `http://localhost:${PORT}`,
    reuseExistingServer: !process.env.CI,
    env: {
      // Another `nuxt dev` (on 3000) may hold the project lock.
      NUXT_IGNORE_LOCK: '1',
      NUXT_PUBLIC_BUYER_HOME_MOCK: 'true',
      NUXT_PUBLIC_MERCHANT_FEED_MOCK: 'true',
      NUXT_PUBLIC_CHAT_MOCK: 'true',
      NUXT_PUBLIC_REALTIME_MOCK: 'true',
    },
  },
})
