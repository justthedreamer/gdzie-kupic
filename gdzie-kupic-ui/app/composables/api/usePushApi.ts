import type { PushSubscriptionBody } from '~/utils/push'

// /api/push — Web Push registration of the signed-in buyer's or merchant's device.
// Contract: planning/phase-7-push-notifications.md (shared contract).
export const usePushApi = () => {
  const api = useApi()

  return {
    /** The VAPID public key (URL-safe base64). Fails when Web Push is not configured on the server. */
    vapidPublicKey: async (): Promise<string> => {
      const res = await api.get<string | { publicKey: string }>('/api/push/vapid-public-key')

      return typeof res === 'string' ? res : res.publicKey
    },

    /** Registers (or re-assigns to the caller) the subscription of this device; idempotent. */
    register: async (subscription: PushSubscriptionBody): Promise<void> => {
      await api.put<unknown>('/api/push/subscription', subscription)
    },

    /** Removes the caller's registration of that endpoint; idempotent. */
    remove: async (endpoint: string): Promise<void> => {
      await api.del<unknown>('/api/push/subscription', { body: { endpoint } })
    },
  }
}
