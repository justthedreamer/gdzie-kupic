// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  devtools: { enabled: true },

  css: ['~/assets/css/main.css'],

  modules: [
    '@nuxt/ui',
    '@pinia/nuxt',
    !process.env.VITEST && '@vite-pwa/nuxt',
    '@nuxtjs/i18n',
    '@nuxt/eslint',
  ],

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
      apiBase: '', // overridden by NUXT_PUBLIC_API_BASE env var
    },
  },
})
