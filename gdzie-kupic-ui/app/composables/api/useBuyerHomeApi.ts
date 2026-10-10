// Data source for the Buyer home page extras: the merchant activity feed.
//
// There is no activity endpoint (the recent chats and the response counts use the real
// chat / status APIs). This returns sample data when `public.buyerHomeMock` is on
// (default in dev) and an empty result otherwise, so production shows the widget's
// empty state. When an endpoint lands, replace the body of `load()` — the widgets only
// consume `BuyerHomeData`.
export const useBuyerHomeApi = () => {
  const useMock = useRuntimeConfig().public.buyerHomeMock

  return {
    load: async (): Promise<BuyerHomeData> => {
      if (!useMock) return emptyBuyerHome()

      const { buildMockBuyerHome } = await import('~/mocks/buyerHome')
      return buildMockBuyerHome()
    },
  }
}
