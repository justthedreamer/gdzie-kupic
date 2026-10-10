// /api/account/profile — the signed-in user's own profile
export interface AccountProfile {
  /** Visible only to the account owner. */
  email: string
  /** The only personal name other users see; `null` when not set. */
  firstName: string | null
  role: 'Buyer' | 'Merchant' | 'Admin'
}

export const useAccountApi = () => {
  const api = useApi()

  return {
    get: (): Promise<AccountProfile> =>
      api.get<AccountProfile>('/api/account/profile'),

    /** Sets the first name; `null` (or an empty string) clears it. 400 when it breaks the name rules. */
    updateFirstName: (firstName: string | null): Promise<AccountProfile> =>
      api.put<AccountProfile>('/api/account/profile', { firstName }),
  }
}
