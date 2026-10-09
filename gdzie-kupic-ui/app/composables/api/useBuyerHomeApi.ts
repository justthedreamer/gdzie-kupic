// Data source for the Buyer home page (requests, merchant activity, chats).
//
// There are no post/response/chat endpoints until Phases 4–5. Until then this
// returns sample data when `public.buyerHomeMock` is on (default in dev) and an
// empty result otherwise, so production shows the widgets' empty states. When
// the endpoints land, replace the body of `load()` — the widgets only consume
// `BuyerHomeData`.
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
