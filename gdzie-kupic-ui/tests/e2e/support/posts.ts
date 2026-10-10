// Fixtures for the posts API (`/api/posts`) in e2e tests.
export const hoursFromNow = (hours: number) => new Date(Date.now() + hours * 3_600_000).toISOString()

export function samplePost(overrides: Record<string, unknown> = {}) {
  return {
    id: 'p1',
    title: 'Rower górski',
    description: 'Rama M, najlepiej aluminiowa.',
    latitude: 50.0617,
    longitude: 19.9373,
    radiusKm: 10,
    category: { id: 'c1', name: 'Sport' },
    tag: { id: 't1', name: 'Rowery' },
    status: 'Active',
    notificationDispatchStatus: 'Dispatched',
    isUrgent: false,
    urgentDeadline: null,
    expiresAt: hoursFromNow(70),
    isLongLived: false,
    createdAt: hoursFromNow(-2),
    notifiedCount: 5,
    ...overrides,
  }
}

export function sampleStatus(overrides: Record<string, unknown> = {}) {
  return {
    notificationDispatchStatus: 'Dispatched',
    notifiedCount: 5,
    checkingCount: 0,
    haveItCount: 0,
    mayHaveItCount: 0,
    canOrderItCount: 0,
    cannotHelpCount: 0,
    isZeroMatch: false,
    ...overrides,
  }
}

export const postPath = (suffix = '') => new RegExp(`^/api/posts/[^/]+${suffix}$`)
