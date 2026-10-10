import type { BuyerHomeData } from '~/utils/buyerHome'

const minutes = (n: number) => n * 60_000

/**
 * Sample merchant activity and chats for the Buyer home page, used until the response/chat
 * endpoints exist (Phase 5). Loaded only when `public.buyerHomeMock` is on.
 */
export function buildMockBuyerHome(now: number = Date.now()): BuyerHomeData {
  const ago = (ms: number) => new Date(now - ms).toISOString()

  return {
    activity: [
      { id: 'act-1', requestId: 'req-1', merchantName: 'Studio Music Kraków', state: 'Have', occurredAt: ago(minutes(2)) },
      { id: 'act-2', requestId: 'req-1', merchantName: 'Audio Pro', state: 'Checking', occurredAt: ago(minutes(5)) },
      { id: 'act-3', requestId: 'req-1', merchantName: 'Muzyczny Raj', state: 'Cannot', occurredAt: ago(minutes(8)) },
      { id: 'act-4', requestId: 'req-1', merchantName: 'Sklep Gitarzysty', state: 'Cannot', occurredAt: ago(minutes(10)) },
      { id: 'act-5', requestId: 'req-2', merchantName: 'Rowery u Marka', state: 'Have', occurredAt: ago(minutes(60)) },
      { id: 'act-6', requestId: 'req-3', merchantName: 'iStore Wrocław', state: 'Have', occurredAt: ago(minutes(30)) },
      { id: 'act-7', requestId: 'req-3', merchantName: 'Telefony 24', state: 'Checking', occurredAt: ago(minutes(45)) },
    ],
    chats: [
      { id: 'chat-1', merchantName: 'Studio Music Kraków', lastMessage: 'Dzień dobry, mamy SM7B na stanie — kiedy możesz odebrać?', sentAt: ago(minutes(2)) },
      { id: 'chat-2', merchantName: 'Rowery u Marka', lastMessage: 'Rower jest dostępny, mogę zostawić go do jutra.', sentAt: ago(minutes(58)) },
      { id: 'chat-3', merchantName: 'iStore Wrocław', lastMessage: 'Potwierdzam, iPhone 14 Pro 256 GB, stan idealny.', sentAt: ago(minutes(28)) },
    ],
  }
}
