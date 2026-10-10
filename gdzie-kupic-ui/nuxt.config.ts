// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  devtools: { enabled: true },

  css: ['~/assets/css/main.css'],

  $development: {
    runtimeConfig: {
      public: { buyerHomeMock: true, merchantFeedMock: true, chatMock: true },
    },
  },

  // Nuxt only scans top-level composables; also auto-import the per-controller
  // API composables (see docs/api.md § Domain composables).
  imports: {
    dirs: ['~/composables/api'],
  },

  modules: [
    '@nuxt/ui',
    '@pinia/nuxt',
    !process.env.VITEST && '@vite-pwa/nuxt',
    '@nuxtjs/i18n',
    '@nuxt/eslint',
  ],

  // ─── UI ───────────────────────────────────────────────────────────────────
  ui: {
    colorMode: false,
  },

  // ─── PWA ──────────────────────────────────────────────────────────────────
  pwa: {
    registerType: 'autoUpdate',
    manifest: {
      name: 'Gdzie Kupić',
      short_name: 'GdzieKupić',
      description: 'Odwrócony marketplace – opisz czego szukasz, a sprzedawcy odpiszą',
      theme_color: '#ffffff',
      background_color: '#ffffff',
      display: 'standalone',
      lang: 'pl',
      icons: [
        { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
        { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
        { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any maskable' },
      ],
    },
    workbox: {
      globPatterns: ['**/*.{js,css,html,ico,png,svg,webp}'],
    },
    devOptions: {
      enabled: true,
      suppressWarnings: true,
      navigateFallback: '/',
      type: 'module',
    },
  },

  // ─── i18n ─────────────────────────────────────────────────────────────────
  i18n: {
    locales: [
      { code: 'pl', name: 'Polski', file: 'pl.json' },
      { code: 'en', name: 'English', file: 'en.json' },
    ],
    defaultLocale: 'pl',
    langDir: 'locales',
    strategy: 'no_prefix',
  },

  // ─── TypeScript ───────────────────────────────────────────────────────────
  typescript: {
    strict: true,
    typeCheck: false, // run explicitly via `npm run typecheck`
  },

  // ─── Runtime config (env vars) ────────────────────────────────────────────
  runtimeConfig: {
    public: {
      apiBase: 'http://localhost:5211',

      // Buyer home page shows sample data until the post/response/chat
      // endpoints exist (Phases 4–5). On in dev, off in production builds;
      // override with NUXT_PUBLIC_BUYER_HOME_MOCK. See docs/api.md.
      buyerHomeMock: false,

      // Same for the Merchant Requests Feed (and request details). Override
      // with NUXT_PUBLIC_MERCHANT_FEED_MOCK. See docs/api.md.
      merchantFeedMock: false,

      // Same for the chat (inbox, threads, unread badge). Override with
      // NUXT_PUBLIC_CHAT_MOCK. See docs/api.md.
      chatMock: false,

      // Pre-generated, non-expiring JWTs for the seeded test accounts — see
      // docs/local-dev.md § Mock Accounts & Pre-Generated Tokens. Used by the
      // dev account switcher (app/components/DevAccountSwitcher.vue) to log
      // in instantly with no API call. Only valid against the default
      // JWT_SECRET; override via NUXT_PUBLIC_MOCK_ACCOUNTS_* env vars if the
      // backend uses a different secret.
      mockAccounts: {
        admin: {
          id: '00000000-0000-0000-0000-000000000001',
          email: 'admin@gdziekupic.local',
          token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDEiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiIsImp0aSI6IjMzODg5MTAyLTUzMWUtNDk1ZS05ZjU1LTdhOTlmODEwNjIwMCIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.n-dmUBeWB2P5DQfNMjJTXo0xwPCdmQWnbDFVM8o39Cs',
        },
        buyer: {
          id: '00000000-0000-0000-0000-000000000002',
          email: 'buyer-test@gdziekupic.local',
          token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDIiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJCdXllciIsImp0aSI6ImM3MTM1ZmVkLTM4NGQtNGRiOS05YmQ4LWJiNjM4MTE2MzJhZSIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.WeftJ8-x9_E-BcNEkNSANy3lv2Nm2U_jdmatbSccR9s',
        },
        merchant: {
          id: '00000000-0000-0000-0000-000000000003',
          email: 'merchant-test@gdziekupic.local',
          token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDMiLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJNZXJjaGFudCIsImp0aSI6IjZmZWU4OWY2LTJhYzAtNDdkMy1hODNhLWMzMzBlZTcwNTczMCIsImV4cCI6NDg5MTM2MzIwMCwiaXNzIjoiR2R6aWVLdXBpY1NlcnZpY2UiLCJhdWQiOiJHZHppZUt1cGljQ2xpZW50In0.KVkFwnZmHta80O6YhrnwR-kp6nhpdTTqKdwFJWhaV-Y',
        },
      },
    },
  },
})
