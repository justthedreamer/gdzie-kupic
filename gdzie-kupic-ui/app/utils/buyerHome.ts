import type { UserRole } from '~/stores/auth'

// Data model behind the Buyer home page. Requests, the status counts and the recent
// chats come from the real APIs; the merchant activity feed has no endpoint yet and is
// supplied by `useBuyerHomeApi`.

export interface BuyerRequestSummary {
  id: string
  title: string
  description: string | null
  /** `null` = unlimited radius. */
  radiusKm: number | null
  category: string
  tag: string | null
  /** ISO timestamp. */
  postedAt: string
  /** The request is still collecting merchant responses. */
  isLive: boolean
  notifiedCount: number
  checkingCount: number
  haveCount: number
  cannotCount: number
}

/** The fields the Live Status widget needs; shared by the Buyer and Merchant views of a request. */
export type LiveCounts = Pick<BuyerRequestSummary, 'isLive' | 'notifiedCount' | 'checkingCount' | 'haveCount' | 'cannotCount'> & {
  /** The split of `haveCount`, when the source has it (the status endpoint does). */
  haveItCount?: number
  mayHaveItCount?: number
  canOrderItCount?: number
}

export type ActivityState = 'Have' | 'Checking' | 'Cannot'

export interface BuyerActivityEvent {
  id: string
  requestId: string
  merchantName: string
  state: ActivityState
  /** ISO timestamp. */
  occurredAt: string
}

export interface BuyerHomeData {
  activity: BuyerActivityEvent[]
}

export const emptyBuyerHome = (): BuyerHomeData => ({ activity: [] })

/** Where a user lands after signing in, and what `/` redirects to. */
export function homePathFor(role: UserRole | undefined): string {
  if (role === 'Buyer') return '/home'
  if (role === 'Merchant') return '/feed'
  if (role === 'Admin') return '/admin/catalogue'
  return '/'
}

export interface StatusBreakdown {
  notified: number
  checking: number
  have: number
  /** The positive answers by kind; `null` when only the total is known. */
  positive: { haveIt: number, mayHaveIt: number, canOrderIt: number } | null
  cannot: number
  none: number
}

/** Merchants that answered in some way (anything except "no response yet"). */
export function respondedCount(request: LiveCounts): number {
  return request.checkingCount + request.haveCount + request.cannotCount
}

/** The four response buckets; they always add up to the notified total. */
export function statusBreakdown(request: LiveCounts): StatusBreakdown {
  const responded = respondedCount(request)
  const notified = Math.max(request.notifiedCount, responded)

  return {
    notified,
    checking: request.checkingCount,
    have: request.haveCount,
    positive: request.haveItCount === undefined || request.mayHaveItCount === undefined || request.canOrderItCount === undefined
      ? null
      : { haveIt: request.haveItCount, mayHaveIt: request.mayHaveItCount, canOrderIt: request.canOrderItCount },
    cannot: request.cannotCount,
    none: notified - responded,
  }
}

export function sortNewestFirst<T extends { postedAt: string }>(items: T[]): T[] {
  return [...items].sort((a, b) => Date.parse(b.postedAt) - Date.parse(a.postedAt))
}

/** The request selected by default: the most recently posted one. */
export function pickDefaultRequest(requests: BuyerRequestSummary[]): BuyerRequestSummary | null {
  return sortNewestFirst(requests)[0] ?? null
}

/** "buyer.test@x.pl" -> "Buyer" — the account has no display name yet. */
export function displayNameFromEmail(email: string | undefined): string {
  const local = (email ?? '').split('@')[0] ?? ''
  const first = local.split(/[._+-]/).find(part => part.length > 0) ?? ''

  return first.charAt(0).toUpperCase() + first.slice(1)
}

const RELATIVE_UNITS: Array<[Intl.RelativeTimeFormatUnit, number]> = [
  ['day', 86_400],
  ['hour', 3_600],
  ['minute', 60],
]

/** "12 min ago" / "12 min temu", in the given locale. */
export function formatRelativeTime(iso: string, locale: string, now: number = Date.now()): string {
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: 'auto', style: 'short' })
  const seconds = Math.round((Date.parse(iso) - now) / 1000)
  const abs = Math.abs(seconds)

  for (const [unit, size] of RELATIVE_UNITS) {
    if (abs >= size) return formatter.format(Math.trunc(seconds / size), unit)
  }

  return formatter.format(0, 'second')
}
