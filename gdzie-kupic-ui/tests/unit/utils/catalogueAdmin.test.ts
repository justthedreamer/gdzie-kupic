import { describe, it, expect } from 'vitest'
import { activeCount, filterByName, filterTags, paginate } from '~/utils/catalogueAdmin'

const tags = [
  { name: 'Smartfon', isDisabled: false },
  { name: 'Telewizor', isDisabled: true },
  { name: 'Słuchawki', isDisabled: false },
  { name: 'Laptop', isDisabled: true },
]

describe('filterByName', () => {
  it('matches case-insensitively on a substring and ignores surrounding spaces', () => {
    expect(filterByName(tags, '  TELE ').map(t => t.name)).toEqual(['Telewizor'])
    expect(filterByName(tags, 'ła')).toEqual([])
  })

  it('keeps everything for an empty query', () => {
    expect(filterByName(tags, '')).toHaveLength(4)
    expect(filterByName(tags, '   ')).toHaveLength(4)
  })
})

describe('filterTags', () => {
  it('filters by status', () => {
    expect(filterTags(tags, '', 'all')).toHaveLength(4)
    expect(filterTags(tags, '', 'active').map(t => t.name)).toEqual(['Smartfon', 'Słuchawki'])
    expect(filterTags(tags, '', 'disabled').map(t => t.name)).toEqual(['Telewizor', 'Laptop'])
  })

  it('combines the status filter with the search', () => {
    expect(filterTags(tags, 'l', 'disabled').map(t => t.name)).toEqual(['Telewizor', 'Laptop'])
    expect(filterTags(tags, 'lap', 'active')).toEqual([])
  })
})

describe('activeCount', () => {
  it('counts enabled items', () => {
    expect(activeCount(tags)).toBe(2)
    expect(activeCount([])).toBe(0)
  })
})

describe('paginate', () => {
  const items = Array.from({ length: 24 }, (_, i) => i + 1)

  it('returns the requested page with its 1-based range', () => {
    const page = paginate(items, 2, 10)

    expect(page.items).toEqual([11, 12, 13, 14, 15, 16, 17, 18, 19, 20])
    expect(page).toMatchObject({ page: 2, pageCount: 3, from: 11, to: 20, total: 24 })
  })

  it('has a shorter last page', () => {
    const page = paginate(items, 3, 10)

    expect(page.items).toEqual([21, 22, 23, 24])
    expect(page).toMatchObject({ from: 21, to: 24 })
  })

  it('clamps an out-of-range page', () => {
    expect(paginate(items, 9, 10).page).toBe(3)
    expect(paginate(items, 0, 10).page).toBe(1)
  })

  it('reports an empty list as page 1 of 1 with a 0–0 range', () => {
    expect(paginate([], 1, 10)).toEqual({ items: [], page: 1, pageCount: 1, from: 0, to: 0, total: 0 })
  })
})
