// /api/account/profile — the signed-in user's own profile
export interface AccountProfile {
  /** Visible only to the account owner. */
  email: string
  /** The only personal name other users see; `null` when not set. */
  firstName: string | null
  role: 'Buyer' | 'Merchant' | 'Admin'
}

// /api/account/notification-settings — contract: planning/phase-7-push-notifications.md
export interface NotificationSettings {
  /** E-mail notifications (buyers: responses and new messages while away; merchants: a regular summary). Off by default. */
  emailEnabled: boolean
}

export const useAccountApi = () => {
  const api = useApi()

  return {
    get: (): Promise<AccountProfile> =>
      api.get<AccountProfile>('/api/account/profile'),

    /** Sets the first name; `null` (or an empty string) clears it. 400 when it breaks the name rules. */
    updateFirstName: (firstName: string | null): Promise<AccountProfile> =>
      api.put<AccountProfile>('/api/account/profile', { firstName }),

    notificationSettings: (): Promise<NotificationSettings> =>
      api.get<NotificationSettings>('/api/account/notification-settings'),

    /** The response body (if any) is not used: the caller keeps the value it sent. */
    updateNotificationSettings: async (settings: NotificationSettings): Promise<void> => {
      await api.put<unknown>('/api/account/notification-settings', settings)
    },
  }
}
