import { describe, it, expect } from 'vitest'
import { useMockAccounts } from '~/composables/useMockAccounts'

describe('useMockAccounts', () => {
  it('returns the Admin, Buyer and Merchant test accounts from runtime config', () => {
    const accounts = useMockAccounts()

    expect(accounts).toHaveLength(3)
    expect(accounts.map(a => a.role)).toEqual(['Admin', 'Buyer', 'Merchant'])

    for (const account of accounts) {
      expect(account.id).toBeTruthy()
      expect(account.email).toBeTruthy()
      expect(account.token).toBeTruthy()
    }
  })
})
