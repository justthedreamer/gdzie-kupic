import { describe, it, expect } from 'vitest'
import type { Post, PostListItem, PostStatusInfo } from '~/composables/api/usePostsApi'
import {
  STATUS_POLL_PENDING_MS,
  STATUS_POLL_SETTLED_MS,
  dismissLongLived,
  isLongLivedDismissed,
  isLongLivedEligible,
  isPostActive,
  postToSummary,
  remainingTime,
  statusPollInterval,
  statusToLiveCounts,
} from '~/utils/posts'

function status(overrides: Partial<PostStatusInfo> = {}): PostStatusInfo {
  return {
    notificationDispatchStatus: 'Dispatched',
    notifiedCount: 0,
    checkingCount: 0,
    haveItCount: 0,
    mayHaveItCount: 0,
    canOrderItCount: 0,
    cannotHelpCount: 0,
    isZeroMatch: true,
    ...overrides,
  }
}

function post(overrides: Partial<Post> = {}): Post {
  return {
    id: 'p1',
    title: 'Rower górski',
    description: null,
    latitude: 50.06,
    longitude: 19.94,
    radiusKm: 10,
    category: { id: 'c1', name: 'Sport' },
    tag: { id: 't1', name: 'Rowery' },
    status: 'Active',
    notificationDispatchStatus: 'Dispatched',
    isUrgent: false,
    urgentDeadline: null,
    expiresAt: '2026-01-04T10:00:00Z',
    isLongLived: false,
    createdAt: '2026-01-01T10:00:00Z',
    ...overrides,
  }
}

describe('statusPollInterval', () => {
  it('polls every 5 s while matching is pending and every 30 s afterwards', () => {
    expect(statusPollInterval('Pending')).toBe(STATUS_POLL_PENDING_MS)
    expect(statusPollInterval('Dispatched')).toBe(STATUS_POLL_SETTLED_MS)
    expect(STATUS_POLL_PENDING_MS).toBe(5_000)
    expect(STATUS_POLL_SETTLED_MS).toBe(30_000)
  })

  it('polls fast until the first status is known', () => {
    expect(statusPollInterval(undefined)).toBe(STATUS_POLL_PENDING_MS)
  })
})

describe('isPostActive', () => {
  it('is true only for Active posts', () => {
    expect(isPostActive(post())).toBe(true)
    for (const ended of ['Fulfilled', 'Closed', 'Expired'] as const) {
      expect(isPostActive(post({ status: ended }))).toBe(false)
    }
  })
})

describe('isLongLivedEligible', () => {
  it('offers the extension for an active, non-urgent, dispatched post nobody was matched for', () => {
    expect(isLongLivedEligible(post(), status())).toBe(true)
  })

  it('does not while matching is still pending, even if nobody was notified yet', () => {
    expect(isLongLivedEligible(post(), status({ notificationDispatchStatus: 'Pending' }))).toBe(false)
  })

  it('does not when merchants were matched', () => {
    expect(isLongLivedEligible(post(), status({ isZeroMatch: false, notifiedCount: 3 }))).toBe(false)
  })

  it('does not for urgent, already long-lived or ended posts', () => {
    expect(isLongLivedEligible(post({ isUrgent: true }), status())).toBe(false)
    expect(isLongLivedEligible(post({ isLongLived: true }), status())).toBe(false)
    expect(isLongLivedEligible(post({ status: 'Expired' }), status())).toBe(false)
  })

  it('does not before the status is known', () => {
    expect(isLongLivedEligible(post(), null)).toBe(false)
  })
})

describe('long-lived dismissal', () => {
  function memoryStorage() {
    const items = new Map<string, string>()

    return {
      getItem: (key: string) => items.get(key) ?? null,
      setItem: (key: string, value: string) => void items.set(key, value),
    }
  }

  it('remembers a dismissal per request', () => {
    const storage = memoryStorage()

    expect(isLongLivedDismissed(storage, 'p1')).toBe(false)
    dismissLongLived(storage, 'p1')
    expect(isLongLivedDismissed(storage, 'p1')).toBe(true)
    expect(isLongLivedDismissed(storage, 'p2')).toBe(false)
  })

  it('copes with missing or failing storage', () => {
    const broken = {
      getItem: () => { throw new Error('denied') },
      setItem: () => { throw new Error('denied') },
    }

    expect(isLongLivedDismissed(null, 'p1')).toBe(false)
    expect(isLongLivedDismissed(broken, 'p1')).toBe(false)
    expect(() => dismissLongLived(null, 'p1')).not.toThrow()
    expect(() => dismissLongLived(broken, 'p1')).not.toThrow()
  })
})

describe('statusToLiveCounts', () => {
  it('groups every positive answer under "have" and keeps the rest as they are', () => {
    const counts = statusToLiveCounts(
      status({ notifiedCount: 10, checkingCount: 2, haveItCount: 1, mayHaveItCount: 2, canOrderItCount: 3, cannotHelpCount: 1, isZeroMatch: false }),
      true,
    )

    expect(counts).toEqual({ isLive: true, notifiedCount: 10, checkingCount: 2, haveCount: 6, cannotCount: 1 })
  })

  it('marks an ended request as not live', () => {
    expect(statusToLiveCounts(status(), false).isLive).toBe(false)
  })
})

describe('postToSummary', () => {
  it('maps a list item to the Buyer home summary', () => {
    const item: PostListItem = { ...post({ radiusKm: null, description: 'Rama M' }), notifiedCount: 7 }

    expect(postToSummary(item)).toEqual({
      id: 'p1',
      title: 'Rower górski',
      description: 'Rama M',
      radiusKm: null,
      category: 'Sport',
      tag: 'Rowery',
      postedAt: '2026-01-01T10:00:00Z',
      isLive: true,
      notifiedCount: 7,
      checkingCount: 0,
      haveCount: 0,
      cannotCount: 0,
    })
  })

  it('is not live once the request ended', () => {
    expect(postToSummary({ ...post({ status: 'Closed' }), notifiedCount: 0 }).isLive).toBe(false)
  })
})

describe('remainingTime', () => {
  const now = Date.parse('2026-01-01T10:00:00Z')

  it('uses the largest whole unit', () => {
    expect(remainingTime('2026-01-04T10:00:00Z', now)).toEqual({ unit: 'day', value: 3 })
    expect(remainingTime('2026-01-01T15:30:00Z', now)).toEqual({ unit: 'hour', value: 5 })
    expect(remainingTime('2026-01-01T10:45:00Z', now)).toEqual({ unit: 'minute', value: 45 })
  })

  it('never shows less than one minute while time is left', () => {
    expect(remainingTime('2026-01-01T10:00:20Z', now)).toEqual({ unit: 'minute', value: 1 })
  })

  it('is null once the time has elapsed or the date is invalid', () => {
    expect(remainingTime('2026-01-01T10:00:00Z', now)).toBeNull()
    expect(remainingTime('2025-12-31T10:00:00Z', now)).toBeNull()
    expect(remainingTime('nonsense', now)).toBeNull()
  })
})
