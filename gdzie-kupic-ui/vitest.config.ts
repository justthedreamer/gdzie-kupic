import { defineVitestConfig } from '@nuxt/test-utils/config'

export default defineVitestConfig({
  test: {
    environment: 'nuxt',
    // Unit tests never open a SignalR connection (signing in would otherwise start one).
    environmentOptions: {
      nuxt: { overrides: { runtimeConfig: { public: { realtimeMock: true } } } },
    },
    include: ['tests/unit/**/*.{test,spec}.ts'],
    coverage: {
      reporter: ['text', 'json', 'html'],
    },
  },
})
