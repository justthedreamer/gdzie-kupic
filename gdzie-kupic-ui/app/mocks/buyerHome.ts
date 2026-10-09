import type { BuyerHomeData } from '~/utils/buyerHome'

const minutes = (n: number) => n * 60_000

/**
 * Sample data for the Buyer home page, used until the post/response/chat
 * endpoints exist (Phases 4–5). Loaded only when `public.buyerHomeMock` is on.
 */
export function buildMockBuyerHome(now: number = Date.now()): BuyerHomeData {
  const ago = (ms: number) => new Date(now - ms).toISOString()

  return {
    requests: [
      {
        id: 'req-1',
        title: 'Szukam mikrofonu Shure SM7B',
        description: 'Nowy lub w stanie bardzo dobrym, z fakturą. Odbiór osobisty.',
        city: 'Kraków',
        country: 'Polska',
        radiusKm: 20,
        budget: 1800,
        category: 'Audio i muzyka',
        tag: 'Mikrofon',
        postedAt: ago(minutes(12)),
        isLive: true,
        notifiedCount: 14,
        checkingCount: 3,
        haveCount: 2,
        cannotCount: 4,
      },
      {
        id: 'req-2',
        title: 'Potrzebuję roweru górskiego dla dziecka 24"',
        description: 'Rama aluminiowa, najlepiej z amortyzatorem.',
        city: 'Warszawa',
        country: 'Polska',
        radiusKm: 10,
        budget: null,
        category: 'Sport i turystyka',
        tag: 'Rower',
        postedAt: ago(minutes(125)),
        isLive: false,
        notifiedCount: 8,
        checkingCount: 0,
        haveCount: 1,
        cannotCount: 7,
      },
      {
        id: 'req-3',
        title: 'Szukam używanego iPhone 14 Pro',
        description: null,
        city: 'Wrocław',
        country: 'Polska',
        radiusKm: 15,
        budget: 3200,
        category: 'Elektronika',
        tag: 'Telefon',
        postedAt: ago(minutes(190)),
        isLive: true,
        notifiedCount: 22,
        checkingCount: 5,
        haveCount: 3,
        cannotCount: 6,
      },
    ],
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
