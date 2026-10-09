export interface MockAccount {
  role: 'Admin' | 'Buyer' | 'Merchant'
  id: string
  email: string
  token: string
}

// Reads the seeded test-account tokens from runtime config (env-driven —
// see nuxt.config.ts `runtimeConfig.public.mockAccounts`), not hardcoded
// inline. Used by DevAccountSwitcher.vue to log in instantly with no API
// call. See docs/local-dev.md § Mock Accounts & Pre-Generated Tokens.
export const useMockAccounts = (): MockAccount[] => {
  const { mockAccounts } = useRuntimeConfig().public

  return [
    { role: 'Admin', ...mockAccounts.admin },
    { role: 'Buyer', ...mockAccounts.buyer },
    { role: 'Merchant', ...mockAccounts.merchant },
  ]
}
