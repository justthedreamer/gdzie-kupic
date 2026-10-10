import { describe, it, expect } from 'vitest'
import {
  defaultFeedFilters,
  FEED_PAGE_SIZE,
  feedCategories,
  feedQuery,
  filterFeed,
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
    category: { id: 'c-audio', name: 'Audio' },
    tag: { id: 't-mic', name: 'Mikrofon' },
    distanceKm: 5,
    buyerRadiusKm: 15,
    buyerName: 'Marek',
    isUrgent: false,
    urgentDeadline: null,
    expiresAt: '2026-01-03T10:00:00Z',
    status: 'Active',
    createdAt: '2026-01-01T10:00:00Z',
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
  const audio = { id: 'c-audio', name: 'Audio' }
  const instruments = { id: 'c-instruments', name: 'Instrumenty' }
  const list = [
    request({ id: 'a', category: audio, distanceKm: 3 }),
    request({ id: 'b', category: instruments, distanceKm: 12 }),
    request({ id: 'c', category: audio, distanceKm: 30 }),
  ]

  it('filters by category id', () => {
    expect(ids(filterFeed(list, filters({ categoryId: 'c-audio' }))).sort()).toEqual(['a', 'c'])
  })

  it('filters by maximum distance, inclusive', () => {
    expect(ids(filterFeed(list, filters({ maxDistanceKm: 12, sort: 'nearest' })))).toEqual(['a', 'b'])
  })

  it('combines category and distance', () => {
    expect(ids(filterFeed(list, filters({ categoryId: 'c-audio', maxDistanceKm: 10 })))).toEqual(['a'])
  })

  it('does not mutate the input', () => {
    const copy = [...list]
    filterFeed(list, filters({ sort: 'nearest' }))
    expect(list).toEqual(copy)
  })
})

describe('filterFeed ordering', () => {
  const list = [
    request({ id: 'old', createdAt: '2026-01-01T08:00:00Z', distanceKm: 1 }),
    request({ id: 'new', createdAt: '2026-01-01T12:00:00Z', distanceKm: 9 }),
    request({ id: 'urgent-old', createdAt: '2026-01-01T06:00:00Z', distanceKm: 20, isUrgent: true }),
    request({ id: 'urgent-new', createdAt: '2026-01-01T09:00:00Z', distanceKm: 15, isUrgent: true }),
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
    const list = [
      request({ category: { id: 'c-instruments', name: 'Instrumenty' } }),
      request({ category: { id: 'c-audio', name: 'Audio' } }),
      request({ category: { id: 'c-audio', name: 'Audio' } }),
    ]
    expect(feedCategories(list)).toEqual([{ id: 'c-audio', name: 'Audio' }, { id: 'c-instruments', name: 'Instrumenty' }])
  })

  it('formats distance per locale', () => {
    expect(formatDistance(5.24, 'en')).toBe('5.2 km')
    expect(formatDistance(5.24, 'pl')).toBe('5,2 km')
  })
})

describe('feedQuery', () => {
  it('sends the filters, the cursor and the default page size', () => {
    const query = feedQuery(filters({ tab: 'responded', categoryId: 'c1', maxDistanceKm: 10, sort: 'nearest' }), 'abc')

    expect(query).toEqual({ tab: 'responded', sort: 'nearest', categoryId: 'c1', maxDistanceKm: 10, cursor: 'abc', limit: FEED_PAGE_SIZE })
  })

  it('leaves out the filters and the cursor that are not set', () => {
    const query = feedQuery(defaultFeedFilters(), null)

    expect(query).toEqual({ tab: 'new', sort: 'newest', categoryId: undefined, maxDistanceKm: undefined, cursor: undefined, limit: FEED_PAGE_SIZE })
  })

  it('passes a custom limit', () => {
    expect(feedQuery(defaultFeedFilters(), null, 50).limit).toBe(50)
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
    expect(urgent?.urgentDeadline).not.toBeNull()
    expect(Date.parse(urgent!.urgentDeadline!)).toBeGreaterThan(Date.parse('2026-03-01T12:00:00Z'))
  })

  it('has the shape of the API contract: no budget, city, verification or live counts', () => {
    for (const item of feed) {
      expect(Object.keys(item).sort()).toEqual([
        'buyerName', 'buyerRadiusKm', 'category', 'createdAt', 'description', 'distanceKm', 'expiresAt', 'id',
        'isUrgent', 'myResponse', 'status', 'tag', 'title', 'urgentDeadline',
      ])
    }
  })

  it('spans more than one mock page so infinite scroll can be tried', () => {
    expect(feed.length).toBeGreaterThan(4)
  })
})
