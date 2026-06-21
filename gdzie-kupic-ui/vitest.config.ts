import { defineVitestConfig } from '@nuxt/test-utils/config'

export default defineVitestConfig({
  test: {
    environment: 'nuxt',
    include: ['tests/unit/**/*.{test,spec}.ts'],
    coverage: {
      reporter: ['text', 'json', 'html'],
    },
  },
})
