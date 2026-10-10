import type {
  NotificationDispatchStatus,
  Post,
  PostListItem,
  PostStatus,
  PostStatusInfo,
} from '~/composables/api/usePostsApi'
import type { BuyerRequestSummary, LiveCounts } from '~/utils/buyerHome'

/** Status refresh interval while merchants are still being matched. */
export const STATUS_POLL_PENDING_MS = 5_000
/** Status refresh interval once matching has finished. */
export const STATUS_POLL_SETTLED_MS = 30_000

/** Days a long-lived request stays open (the server owns the real value; this is only the label). */
export const LONG_LIVED_DAYS = 14

export function statusPollInterval(dispatchStatus: NotificationDispatchStatus | undefined): number {
  return dispatchStatus === 'Dispatched' ? STATUS_POLL_SETTLED_MS : STATUS_POLL_PENDING_MS
}

export const isPostActive = (post: Pick<Post, 'status'>): boolean => post.status === 'Active'

export type PostBadgeColor = 'success' | 'primary' | 'neutral' | 'warning'

export const POST_STATUS_COLOR: Record<PostStatus, PostBadgeColor> = {
  Active: 'success',
  Fulfilled: 'primary',
  Closed: 'neutral',
  Expired: 'warning',
}

/** Whether the "extend to 14 days" offer applies. Mirrors the server's eligibility rule. */
export function isLongLivedEligible(
  post: Pick<Post, 'status' | 'isUrgent' | 'isLongLived'>,
  status: Pick<PostStatusInfo, 'notificationDispatchStatus' | 'isZeroMatch'> | null | undefined,
): boolean {
  return post.status === 'Active'
    && !post.isUrgent
    && !post.isLongLived
    && status?.notificationDispatchStatus === 'Dispatched'
    && status.isZeroMatch
}

type DismissStorage = Pick<Storage, 'getItem' | 'setItem'>

const dismissKey = (postId: string) => `gdzie-kupic:long-lived-dismissed:${postId}`

/** Browser storage can be unavailable (private mode, disabled); the popup then simply may reappear. */
export function browserStorage(): DismissStorage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  }
  catch {
    return null
  }
}

export function isLongLivedDismissed(storage: DismissStorage | null, postId: string): boolean {
  try {
    return storage?.getItem(dismissKey(postId)) === '1'
  }
  catch {
    return false
  }
}

export function dismissLongLived(storage: DismissStorage | null, postId: string): void {
  try {
    storage?.setItem(dismissKey(postId), '1')
  }
  catch {
    // Not being able to remember the choice must not break the page.
  }
}

/** Live Status counts for a request. Responses stay 0 until Phase 5; "have" groups every positive answer. */
export function statusToLiveCounts(status: PostStatusInfo, isLive: boolean): LiveCounts {
  return {
    isLive,
    notifiedCount: status.notifiedCount,
    checkingCount: status.checkingCount,
    haveCount: status.haveItCount + status.mayHaveItCount + status.canOrderItCount,
    cannotCount: status.cannotHelpCount,
  }
}

/** List item -> Buyer home summary. Response counts come from the status endpoint, so they start at 0. */
export function postToSummary(post: PostListItem): BuyerRequestSummary {
  return {
    id: post.id,
    title: post.title,
    description: post.description,
    radiusKm: post.radiusKm,
    category: post.category.name,
    tag: post.tag.name,
    postedAt: post.createdAt,
    isLive: post.status === 'Active',
    notifiedCount: post.notifiedCount,
    checkingCount: 0,
    haveCount: 0,
    cannotCount: 0,
  }
}

export type RemainingUnit = 'day' | 'hour' | 'minute'

export interface Remaining {
  unit: RemainingUnit
  value: number
}

const MINUTE = 60_000
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR

/** Time left until `expiresAt`, in its largest whole unit (at least one minute); `null` once elapsed. */
export function remainingTime(expiresAt: string, now: number = Date.now()): Remaining | null {
  const ms = Date.parse(expiresAt) - now
  if (Number.isNaN(ms) || ms <= 0) return null

  if (ms >= DAY) return { unit: 'day', value: Math.floor(ms / DAY) }
  if (ms >= HOUR) return { unit: 'hour', value: Math.floor(ms / HOUR) }

  return { unit: 'minute', value: Math.max(1, Math.floor(ms / MINUTE)) }
}
