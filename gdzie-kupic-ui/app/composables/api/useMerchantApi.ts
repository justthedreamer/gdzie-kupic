// /api/merchant/* — onboarding, profile and category/tag subscriptions
export interface MerchantBranch {
  id: string
  displayName: string
  latitude: number
  longitude: number
  addressDisplayName: string | null
  phone: string | null
  website: string | null
}

export interface MerchantProfile {
  merchantId: string
  name: string
  description: string | null
  branch: MerchantBranch
}

export interface OnboardingRequest {
  name: string
  description?: string
  branch: {
    displayName: string
    phone?: string
    website?: string
  } & LocationPayload
}

export interface MerchantSubscription {
  id: string
  categoryId: string
  tagId: string | null
}

export const useMerchantApi = () => {
  const api = useApi()

  const subscribe = (target: SubscriptionTarget): Promise<MerchantSubscription> =>
    api.post<MerchantSubscription>('/api/merchant/subscriptions', {
      categoryId: target.categoryId,
      ...(target.tagId ? { tagId: target.tagId } : {}),
    })

  return {
    /** Rejects with a 404 FetchError when the merchant has not onboarded yet. */
    getMe: (): Promise<MerchantProfile> =>
      api.get<MerchantProfile>('/api/merchant/me'),

    onboard: (data: OnboardingRequest): Promise<MerchantProfile> =>
      api.post<MerchantProfile>('/api/merchant/onboarding', data),

    listSubscriptions: (): Promise<MerchantSubscription[]> =>
      api.get<MerchantSubscription[]>('/api/merchant/subscriptions'),

    subscribe,

    unsubscribe: (id: string): Promise<void> =>
      api.del<undefined>(`/api/merchant/subscriptions/${id}`),

    /**
     * Subscribes to every target and resolves to the ones that failed.
     * A 409 (already subscribed) counts as success, so retrying a partially
     * failed batch is safe.
     */
    subscribeMany: async (targets: SubscriptionTarget[]): Promise<SubscriptionTarget[]> => {
      const failed: SubscriptionTarget[] = []

      for (const target of targets) {
        try {
          await subscribe(target)
        }
        catch (err) {
          if (parseApiError(err).status !== 409) failed.push(target)
        }
      }

      return failed
    },
  }
}
