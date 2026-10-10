import { describe, it, expect } from 'vitest'
import {
  displayNameFromEmail,
  formatRelativeTime,
  homePathFor,
  pickDefaultRequest,
  respondedCount,
  sortNewestFirst,
  statusBreakdown,
  type BuyerRequestSummary,
} from '~/utils/buyerHome'

function request(overrides: Partial<BuyerRequestSummary> = {}): BuyerRequestSummary {
  return {
    id: 'r1',
    title: 'Mikrofon',
    description: null,
    radiusKm: 10,
    category: 'Audio',
    tag: null,
    postedAt: '2026-01-01T10:00:00Z',
    isLive: true,
    notifiedCount: 10,
    checkingCount: 2,
    haveCount: 3,
    cannotCount: 1,
    ...overrides,
  }
}

describe('homePathFor', () => {
  it('sends Buyers to /home, Merchants to /feed, Admins to the catalogue and everyone else to /', () => {
    expect(homePathFor('Buyer')).toBe('/home')
    expect(homePathFor('Merchant')).toBe('/feed')
    expect(homePathFor('Admin')).toBe('/admin/catalogue')
    expect(homePathFor(undefined)).toBe('/')
  })
})

describe('statusBreakdown', () => {
  it('derives "no response yet" so the buckets add up to the notified total', () => {
    const breakdown = statusBreakdown(request())

    expect(breakdown).toEqual({ notified: 10, checking: 2, have: 3, cannot: 1, none: 4 })
    expect(breakdown.checking + breakdown.have + breakdown.cannot + breakdown.none).toBe(breakdown.notified)
  })

  it('never reports a negative "no response" count when responses exceed the notified total', () => {
    const breakdown = statusBreakdown(request({ notifiedCount: 3 }))

    expect(breakdown.none).toBe(0)
    expect(breakdown.notified).toBe(6)
  })

  it('counts responded merchants', () => {
    expect(respondedCount(request())).toBe(6)
  })
})

describe('request selection', () => {
  const older = request({ id: 'old', postedAt: '2026-01-01T08:00:00Z' })
  const newest = request({ id: 'new', postedAt: '2026-01-01T12:00:00Z' })
  const middle = request({ id: 'mid', postedAt: '2026-01-01T10:00:00Z' })

  it('sorts newest first without mutating the input', () => {
    const input = [older, newest, middle]

    expect(sortNewestFirst(input).map(r => r.id)).toEqual(['new', 'mid', 'old'])
    expect(input.map(r => r.id)).toEqual(['old', 'new', 'mid'])
  })

  it('picks the most recently posted request by default', () => {
    expect(pickDefaultRequest([older, newest, middle])?.id).toBe('new')
  })

  it('returns null when there are no requests', () => {
    expect(pickDefaultRequest([])).toBeNull()
  })
})

describe('displayNameFromEmail', () => {
  it('uses the capitalised first part of the local name', () => {
    expect(displayNameFromEmail('buyer-test@gdziekupic.local')).toBe('Buyer')
    expect(displayNameFromEmail('anna.nowak@example.com')).toBe('Anna')
  })

  it('copes with a missing address', () => {
    expect(displayNameFromEmail(undefined)).toBe('')
  })
})

describe('formatRelativeTime', () => {
  const now = Date.parse('2026-01-01T12:00:00Z')

  it('formats minutes, hours and days in the given locale', () => {
    expect(formatRelativeTime('2026-01-01T11:48:00Z', 'en', now)).toMatch(/12\s*min/)
    expect(formatRelativeTime('2026-01-01T09:00:00Z', 'en', now)).toMatch(/3\s*hr/)
    expect(formatRelativeTime('2025-12-30T12:00:00Z', 'en', now)).toMatch(/2\s*days/)
    expect(formatRelativeTime('2026-01-01T11:48:00Z', 'pl', now)).toMatch(/12\s*min/)
  })

  it('says "now" for very recent timestamps', () => {
    expect(formatRelativeTime('2026-01-01T11:59:40Z', 'en', now)).toBe('now')
  })
})
