// Data source for the Merchant Requests Feed.
//
// There are no post/matching/response endpoints until Phases 4–5. Until then
// this returns sample data when `public.merchantFeedMock` is on (default in
// dev) and an empty feed otherwise, so production shows the empty state. The
// merchant's own answers are remembered here for the life of the page so they
// survive a refresh of the feed. When the endpoints land, replace the bodies
// of `load()` and `respond()` — the pages only consume `MerchantFeedRequest`.
const mockResponses = new Map<string, MerchantResponse>()

export const useMerchantFeedApi = () => {
  const useMock = useRuntimeConfig().public.merchantFeedMock

  return {
    load: async (): Promise<MerchantFeedRequest[]> => {
      if (!useMock) return []

      const { buildMockMerchantFeed } = await import('~/mocks/merchantFeed')
      return buildMockMerchantFeed().map(request => ({
        ...request,
        myResponse: mockResponses.get(request.id) ?? request.myResponse,
      }))
    },

    respond: async (id: string, state: MerchantResponse): Promise<void> => {
      mockResponses.set(id, state)
    },
  }
}
