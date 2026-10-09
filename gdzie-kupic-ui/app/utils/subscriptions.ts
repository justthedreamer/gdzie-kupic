/** A merchant subscription target — `tagId: null` means the whole category. */
export interface SubscriptionTarget {
  categoryId: string
  tagId: string | null
}

export function isCategorySelected(selection: SubscriptionTarget[], categoryId: string): boolean {
  return selection.some(s => s.categoryId === categoryId && s.tagId === null)
}

export function isTagSelected(selection: SubscriptionTarget[], categoryId: string, tagId: string): boolean {
  return selection.some(s => s.categoryId === categoryId && s.tagId === tagId)
}

/**
 * Toggles a whole-category subscription. Selecting a category drops that
 * category's individual tags, since the whole category already covers them.
 */
export function toggleCategory(selection: SubscriptionTarget[], categoryId: string): SubscriptionTarget[] {
  if (isCategorySelected(selection, categoryId)) {
    return selection.filter(s => !(s.categoryId === categoryId && s.tagId === null))
  }
  return [
    ...selection.filter(s => s.categoryId !== categoryId),
    { categoryId, tagId: null },
  ]
}

export function toggleTag(selection: SubscriptionTarget[], categoryId: string, tagId: string): SubscriptionTarget[] {
  if (isTagSelected(selection, categoryId, tagId)) {
    return selection.filter(s => !(s.categoryId === categoryId && s.tagId === tagId))
  }
  return [...selection, { categoryId, tagId }]
}

export function subscriptionKey(target: SubscriptionTarget): string {
  return `${target.categoryId}:${target.tagId ?? '*'}`
}
