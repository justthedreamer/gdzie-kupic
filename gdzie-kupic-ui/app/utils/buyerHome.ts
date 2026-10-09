import type { UserRole } from '~/stores/auth'

// Data model behind the Buyer home page. Posts, merchant responses and chats
// get real endpoints in Phases 4–5; until then `useBuyerHomeApi` supplies it.

export interface BuyerRequestSummary {
  id: string
  title: string
  description: string | null
  city: string
  country: string
  radiusKm: number
  /** Optional, in PLN. */
  budget: number | null
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
export type LiveCounts = Pick<BuyerRequestSummary, 'isLive' | 'notifiedCount' | 'checkingCount' | 'haveCount' | 'cannotCount'>

export type ActivityState = 'Have' | 'Checking' | 'Cannot'

export interface BuyerActivityEvent {
  id: string
  requestId: string
  merchantName: string
  state: ActivityState
  /** ISO timestamp. */
  occurredAt: string
}

export interface BuyerChatPreview {
  id: string
  merchantName: string
  lastMessage: string
  /** ISO timestamp. */
  sentAt: string
}

export interface BuyerHomeData {
  requests: BuyerRequestSummary[]
  activity: BuyerActivityEvent[]
  chats: BuyerChatPreview[]
}

export const emptyBuyerHome = (): BuyerHomeData => ({ requests: [], activity: [], chats: [] })

/** Where a user lands after signing in, and what `/` redirects to. */
export function homePathFor(role: UserRole | undefined): string {
  if (role === 'Buyer') return '/home'
  if (role === 'Merchant') return '/feed'
  return '/'
}

export interface StatusBreakdown {
  notified: number
  checking: number
  have: number
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
