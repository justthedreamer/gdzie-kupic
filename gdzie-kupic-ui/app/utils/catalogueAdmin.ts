// Pure helpers of the Admin catalogue screen: searching, status filter and
// client-side pagination (the API returns the whole catalogue in one call).

export type TagStatusFilter = 'all' | 'active' | 'disabled'

export const TAG_STATUS_FILTERS: TagStatusFilter[] = ['all', 'active', 'disabled']
export const PAGE_SIZES = [10, 25, 50]
export const NAME_MAX_LENGTH = 100

interface Named {
  name: string
}

interface Switchable extends Named {
  isDisabled: boolean
}

function matches(item: Named, query: string): boolean {
  const needle = query.trim().toLocaleLowerCase()
  return !needle || item.name.toLocaleLowerCase().includes(needle)
}

export function filterByName<T extends Named>(items: T[], query: string): T[] {
  return items.filter(item => matches(item, query))
}

export function filterTags<T extends Switchable>(tags: T[], query: string, status: TagStatusFilter): T[] {
  return tags.filter((tag) => {
    if (status === 'active' && tag.isDisabled) return false
    if (status === 'disabled' && !tag.isDisabled) return false
    return matches(tag, query)
  })
}

export function activeCount(items: Switchable[]): number {
  return items.filter(item => !item.isDisabled).length
}

export interface PageSlice<T> {
  items: T[]
  /** 1-based, clamped to the available pages. */
  page: number
  pageCount: number
  /** 1-based position of the first / last item on the page; both 0 when empty. */
  from: number
  to: number
  total: number
}

export function paginate<T>(items: T[], page: number, pageSize: number): PageSlice<T> {
  const total = items.length
  const pageCount = Math.max(1, Math.ceil(total / pageSize))
  const current = Math.min(Math.max(1, page), pageCount)
  const start = (current - 1) * pageSize
  const slice = items.slice(start, start + pageSize)

  return {
    items: slice,
    page: current,
    pageCount,
    from: slice.length ? start + 1 : 0,
    to: slice.length ? start + slice.length : 0,
    total,
  }
}
