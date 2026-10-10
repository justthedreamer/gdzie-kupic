import { describe, it, expect } from 'vitest'
import {
  findSubscription,
  tagSubscriptionsOf,
  isCategorySelected,
  isTagSelected,
  subscriptionKey,
  toggleCategory,
  toggleTag,
  type SubscriptionTarget,
} from '~/utils/subscriptions'

describe('subscription selection', () => {
  it('selects and deselects a whole category', () => {
    let selection: SubscriptionTarget[] = []

    selection = toggleCategory(selection, 'c1')
    expect(isCategorySelected(selection, 'c1')).toBe(true)

    selection = toggleCategory(selection, 'c1')
    expect(selection).toEqual([])
  })

  it('selects and deselects individual tags', () => {
    let selection: SubscriptionTarget[] = []

    selection = toggleTag(selection, 'c1', 't1')
    selection = toggleTag(selection, 'c1', 't2')
    expect(isTagSelected(selection, 'c1', 't1')).toBe(true)

    selection = toggleTag(selection, 'c1', 't1')
    expect(selection).toEqual([{ categoryId: 'c1', tagId: 't2' }])
  })

  it('drops a category\'s tags when the whole category is selected', () => {
    let selection: SubscriptionTarget[] = [
      { categoryId: 'c1', tagId: 't1' },
      { categoryId: 'c2', tagId: 't9' },
    ]

    selection = toggleCategory(selection, 'c1')

    expect(selection).toEqual([
      { categoryId: 'c2', tagId: 't9' },
      { categoryId: 'c1', tagId: null },
    ])
  })

  it('builds distinct keys for category and tag targets', () => {
    expect(subscriptionKey({ categoryId: 'c1', tagId: null })).not.toBe(subscriptionKey({ categoryId: 'c1', tagId: 't1' }))
  })
})

describe('stored subscriptions', () => {
  const stored = [
    { id: 's1', categoryId: 'c1', tagId: null },
    { id: 's2', categoryId: 'c2', tagId: 't1' },
    { id: 's3', categoryId: 'c2', tagId: 't2' },
  ]

  it('finds a subscription by category or tag', () => {
    expect(findSubscription(stored, 'c1', null)?.id).toBe('s1')
    expect(findSubscription(stored, 'c2', 't2')?.id).toBe('s3')
    expect(findSubscription(stored, 'c2', null)).toBeUndefined()
  })

  it('lists only tag-level subscriptions of a category', () => {
    expect(tagSubscriptionsOf(stored, 'c2').map(s => s.id)).toEqual(['s2', 's3'])
    expect(tagSubscriptionsOf(stored, 'c1')).toEqual([])
  })
})
