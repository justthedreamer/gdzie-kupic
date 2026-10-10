import type { BuyerHomeData } from '~/utils/buyerHome'

const minutes = (n: number) => n * 60_000

/**
 * Sample merchant activity for the Buyer home page (there is no activity endpoint yet).
 * Loaded only when `public.buyerHomeMock` is on.
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
  }
}
