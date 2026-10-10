import { filterFeed, type FeedFilters, type FeedPage, type MerchantFeedRequest } from '~/utils/merchantFeed'

/**
 * A stand-in for the feed endpoint: filters and sorts like the backend and
 * pages with an index cursor. `source` is read on every call, so a test can
 * change the "server" data between requests.
 */
export function serveFeed(source: () => MerchantFeedRequest[], pageSize: number) {
  return async (filters: FeedFilters, cursor: string | null): Promise<FeedPage> => {
    const matching = filterFeed(source(), filters)
    const start = cursor === null ? 0 : Number(cursor)
    const end = start + pageSize
    return { items: matching.slice(start, end), nextCursor: end < matching.length ? String(end) : null }
  }
}
