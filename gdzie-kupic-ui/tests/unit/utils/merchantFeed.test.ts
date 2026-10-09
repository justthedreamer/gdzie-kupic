import { describe, it, expect } from 'vitest'
import {
  defaultFeedFilters,
  feedCategories,
  filterFeed,
  formatBudget,
  formatDistance,
  MERCHANT_RESPONSES,
  RESPONSE_COLOR,
  unansweredCount,
  type FeedFilters,
  type MerchantFeedRequest,
} from '~/utils/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'

function request(overrides: Partial<MerchantFeedRequest> = {}): MerchantFeedRequest {
  return {
    id: 'r1',
    title: 'Mikrofon',
    description: null,
    city: 'Kraków',
    distanceKm: 5,
    buyerRadiusKm: 15,
    budget: null,
    category: 'Audio',
    tag: null,
    buyerName: 'Marek',
    buyerVerified: true,
    postedAt: '2026-01-01T10:00:00Z',
    isUrgent: false,
    deadline: null,
    isLive: true,
    notifiedCount: 10,
    checkingCount: 1,
    haveCount: 1,
    cannotCount: 1,
    myResponse: null,
    ...overrides,
  }
}

const filters = (overrides: Partial<FeedFilters> = {}): FeedFilters => ({ ...defaultFeedFilters(), ...overrides })
const ids = (items: MerchantFeedRequest[]) => items.map(item => item.id)

describe('filterFeed tabs', () => {
  const list = [
    request({ id: 'new' }),
    request({ id: 'have', myResponse: 'HaveIt' }),
    request({ id: 'cant', myResponse: 'CantHelp' }),
  ]

  it('"new" keeps only unanswered requests (the default tab)', () => {
    expect(ids(filterFeed(list, defaultFeedFilters()))).toEqual(['new'])
  })

  it('"responded" keeps only answered requests, including "can\'t help"', () => {
    expect(ids(filterFeed(list, filters({ tab: 'responded' }))).sort()).toEqual(['cant', 'have'])
  })

  it('"all" keeps everything', () => {
    expect(filterFeed(list, filters({ tab: 'all' }))).toHaveLength(3)
  })
})

describe('filterFeed filters', () => {
  const list = [
    request({ id: 'a', category: 'Audio', distanceKm: 3 }),
    request({ id: 'b', category: 'Instrumenty', distanceKm: 12 }),
    request({ id: 'c', category: 'Audio', distanceKm: 30 }),
  ]

  it('filters by category', () => {
    expect(ids(filterFeed(list, filters({ category: 'Audio' })))).toEqual(expect.arrayContaining(['a', 'c']))
    expect(filterFeed(list, filters({ category: 'Audio' }))).toHaveLength(2)
  })

  it('filters by maximum distance, inclusive', () => {
    expect(ids(filterFeed(list, filters({ maxDistanceKm: 12, sort: 'nearest' })))).toEqual(['a', 'b'])
  })

  it('combines category and distance', () => {
    expect(ids(filterFeed(list, filters({ category: 'Audio', maxDistanceKm: 10 })))).toEqual(['a'])
  })

  it('does not mutate the input', () => {
    const copy = [...list]
    filterFeed(list, filters({ sort: 'nearest' }))
    expect(list).toEqual(copy)
  })
})

describe('filterFeed ordering', () => {
  const list = [
    request({ id: 'old', postedAt: '2026-01-01T08:00:00Z', distanceKm: 1 }),
    request({ id: 'new', postedAt: '2026-01-01T12:00:00Z', distanceKm: 9 }),
    request({ id: 'urgent-old', postedAt: '2026-01-01T06:00:00Z', distanceKm: 20, isUrgent: true }),
    request({ id: 'urgent-new', postedAt: '2026-01-01T09:00:00Z', distanceKm: 15, isUrgent: true }),
  ]

  it('"newest" lists urgent requests first, then newest first within each group (FR-FEED-3)', () => {
    expect(ids(filterFeed(list, filters()))).toEqual(['urgent-new', 'urgent-old', 'new', 'old'])
  })

  it('"nearest" orders by distance regardless of urgency', () => {
    expect(ids(filterFeed(list, filters({ sort: 'nearest' })))).toEqual(['old', 'new', 'urgent-new', 'urgent-old'])
  })
})

describe('feed helpers', () => {
  it('counts unanswered requests', () => {
    expect(unansweredCount([request(), request({ myResponse: 'MayHaveIt' }), request()])).toBe(2)
  })

  it('counts a CanOrderIt request as answered', () => {
    expect(unansweredCount([request(), request({ myResponse: 'CanOrderIt' })])).toBe(1)
  })

  it('defines all four FR-RESP-1 states, each with its own colour', () => {
    expect(MERCHANT_RESPONSES).toEqual(['HaveIt', 'MayHaveIt', 'CanOrderIt', 'CantHelp'])
    expect(new Set(MERCHANT_RESPONSES.map(state => RESPONSE_COLOR[state])).size).toBe(4)
  })

  it('lists distinct categories alphabetically', () => {
    const list = [request({ category: 'Instrumenty' }), request({ category: 'Audio' }), request({ category: 'Audio' })]
    expect(feedCategories(list)).toEqual(['Audio', 'Instrumenty'])
  })

  it('formats distance and budget per locale', () => {
    expect(formatDistance(5.24, 'en')).toBe('5.2 km')
    expect(formatDistance(5.24, 'pl')).toBe('5,2 km')
    expect(formatBudget(2000, 'en')).toContain('2,000')
  })
})

describe('buildMockMerchantFeed', () => {
  const feed = buildMockMerchantFeed(Date.parse('2026-03-01T12:00:00Z'))

  it('has unique ids and a mix of answered and unanswered requests', () => {
    expect(new Set(feed.map(item => item.id)).size).toBe(feed.length)
    expect(unansweredCount(feed)).toBeGreaterThan(0)
    expect(unansweredCount(feed)).toBeLessThan(feed.length)
  })

  it('contains an urgent request with a deadline in the future', () => {
    const urgent = feed.find(item => item.isUrgent)
    expect(urgent?.deadline).not.toBeNull()
    expect(Date.parse(urgent!.deadline!)).toBeGreaterThan(Date.parse('2026-03-01T12:00:00Z'))
  })

  it('keeps the status counts within the notified total', () => {
    for (const item of feed) {
      expect(item.checkingCount + item.haveCount + item.cannotCount).toBeLessThanOrEqual(item.notifiedCount)
    }
  })
})
