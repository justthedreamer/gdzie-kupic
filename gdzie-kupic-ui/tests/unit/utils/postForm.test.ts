import { describe, expect, it } from 'vitest'
import {
  buildCreatePostRequest,
  deadlineBounds,
  emptyPostForm,
  enabledCategories,
  enabledTags,
  parseCustomRadius,
  parseDeadline,
  resolveRadiusKm,
  toDateTimeLocal,
  validateDeadline,
  validatePostForm,
  type PostFormState,
} from '~/utils/postForm'

const NOW = new Date(2026, 9, 10, 12, 0, 0)

const categories: CatalogueCategory[] = [
  {
    id: 'c1',
    name: 'Sport',
    isDisabled: false,
    tags: [
      { id: 't1', name: 'Rowery', isDisabled: false },
      { id: 't2', name: 'Narty', isDisabled: true },
    ],
  },
  {
    id: 'c2',
    name: 'Wyłączona',
    isDisabled: true,
    tags: [{ id: 't3', name: 'Cokolwiek', isDisabled: false }],
  },
]

function validForm(overrides: Partial<PostFormState> = {}): PostFormState {
  return {
    ...emptyPostForm(),
    location: { latitude: 50.06, longitude: 19.94, source: 'saved', savedId: 's1' },
    categoryId: 'c1',
    tagId: 't1',
    title: 'Rower górski',
    ...overrides,
  }
}

const codes = (state: PostFormState) => validatePostForm(state, categories, NOW).map(error => error.code)

describe('parseCustomRadius', () => {
  it.each([
    ['15', 15],
    [' 2.5 ', 2.5],
    ['2,5', 2.5],
    ['1000', 1000],
    ['0.1', 0.1],
  ])('accepts %s', (text, expected) => {
    expect(parseCustomRadius(text)).toBe(expected)
  })

  it.each(['', '  ', '0', '-3', 'abc', 'Infinity'])('rejects "%s"', (text) => {
    expect(parseCustomRadius(text)).toBeNull()
  })
})

describe('resolveRadiusKm', () => {
  it('uses the preset value', () => {
    expect(resolveRadiusKm('25', '')).toEqual({ valid: true, radiusKm: 25 })
  })

  it('sends a null radius for unlimited', () => {
    expect(resolveRadiusKm('unlimited', '')).toEqual({ valid: true, radiusKm: null })
  })

  it('uses the custom value and ignores it for other modes', () => {
    expect(resolveRadiusKm('custom', '12')).toEqual({ valid: true, radiusKm: 12 })
    expect(resolveRadiusKm('5', '12')).toEqual({ valid: true, radiusKm: 5 })
  })

  it('rejects an invalid custom value', () => {
    expect(resolveRadiusKm('custom', '0')).toEqual({ valid: false })
    expect(resolveRadiusKm('custom', '')).toEqual({ valid: false })
  })
})

describe('deadline helpers', () => {
  it('formats and parses local datetime values', () => {
    const value = toDateTimeLocal(new Date(2026, 0, 5, 7, 3))
    expect(value).toBe('2026-01-05T07:03')
    expect(parseDeadline(value)?.getTime()).toBe(new Date(2026, 0, 5, 7, 3).getTime())
  })

  it('returns null for empty or malformed values', () => {
    expect(parseDeadline('')).toBeNull()
    expect(parseDeadline('jutro')).toBeNull()
  })

  it('computes the 1–72 h bounds', () => {
    expect(deadlineBounds(NOW)).toEqual({ min: '2026-10-10T13:00', max: '2026-10-13T12:00' })
  })

  it('rounds the lower bound up to the next minute', () => {
    expect(deadlineBounds(new Date(2026, 9, 10, 12, 0, 30)).min).toBe('2026-10-10T13:01')
  })

  it('validates the window (inclusive of 1 h and 72 h)', () => {
    expect(validateDeadline('', NOW)).toBe('deadline_required')
    expect(validateDeadline('x', NOW)).toBe('deadline_invalid')
    expect(validateDeadline('2026-10-10T12:59', NOW)).toBe('deadline_too_soon')
    expect(validateDeadline('2026-10-10T13:00', NOW)).toBeNull()
    expect(validateDeadline('2026-10-13T12:00', NOW)).toBeNull()
    expect(validateDeadline('2026-10-13T12:01', NOW)).toBe('deadline_too_far')
  })
})

describe('catalogue helpers', () => {
  it('drops disabled categories and tags', () => {
    expect(enabledCategories(categories).map(c => c.id)).toEqual(['c1'])
    expect(enabledTags(categories, 'c1').map(t => t.id)).toEqual(['t1'])
  })

  it('offers no tags for a disabled or unknown category', () => {
    expect(enabledTags(categories, 'c2')).toEqual([])
    expect(enabledTags(categories, 'nope')).toEqual([])
  })
})

describe('validatePostForm', () => {
  it('accepts a valid form', () => {
    expect(codes(validForm())).toEqual([])
  })

  it('requires everything an empty form lacks', () => {
    expect(codes(emptyPostForm())).toEqual(['location_required', 'category_required', 'title_required'])
  })

  it('requires a tag once a category is chosen', () => {
    expect(codes(validForm({ tagId: '' }))).toEqual(['tag_required'])
  })

  it('rejects a tag outside the chosen category or a disabled one', () => {
    expect(codes(validForm({ tagId: 't3' }))).toEqual(['tag_invalid'])
    expect(codes(validForm({ tagId: 't2' }))).toEqual(['tag_invalid'])
  })

  it('rejects a disabled category', () => {
    expect(codes(validForm({ categoryId: 'c2', tagId: 't3' }))).toEqual(['tag_invalid'])
  })

  it('rejects a non-positive custom radius', () => {
    expect(codes(validForm({ radiusMode: 'custom', customRadius: '0' }))).toEqual(['radius_invalid'])
    expect(codes(validForm({ radiusMode: 'custom', customRadius: '40' }))).toEqual([])
  })

  it('accepts unlimited radius', () => {
    expect(codes(validForm({ radiusMode: 'unlimited' }))).toEqual([])
  })

  it('enforces title and description limits', () => {
    expect(codes(validForm({ title: '   ' }))).toEqual(['title_required'])
    expect(codes(validForm({ title: 'a'.repeat(121) }))).toEqual(['title_too_long'])
    expect(codes(validForm({ title: 'a'.repeat(120) }))).toEqual([])
    expect(codes(validForm({ description: 'a'.repeat(2001) }))).toEqual(['description_too_long'])
  })

  it('validates the deadline only for urgent requests', () => {
    expect(codes(validForm({ urgent: false, deadline: 'junk' }))).toEqual([])
    expect(codes(validForm({ urgent: true, deadline: '' }))).toEqual(['deadline_required'])
    expect(codes(validForm({ urgent: true, deadline: '2026-10-11T09:00' }))).toEqual([])
  })
})

describe('buildCreatePostRequest', () => {
  it('builds the body for a plain request', () => {
    expect(buildCreatePostRequest(validForm({ title: '  Rower  ', description: '  ' }))).toEqual({
      latitude: 50.06,
      longitude: 19.94,
      radiusKm: 10,
      categoryId: 'c1',
      tagId: 't1',
      title: 'Rower',
    })
  })

  it('sends a null radius for unlimited and the custom value otherwise', () => {
    expect(buildCreatePostRequest(validForm({ radiusMode: 'unlimited' })).radiusKm).toBeNull()
    expect(buildCreatePostRequest(validForm({ radiusMode: 'custom', customRadius: '2,5' })).radiusKm).toBe(2.5)
  })

  it('adds the description and an ISO deadline for urgent requests', () => {
    const body = buildCreatePostRequest(validForm({
      description: ' Z kołami 29" ',
      urgent: true,
      deadline: '2026-10-11T09:00',
    }))

    expect(body.description).toBe('Z kołami 29"')
    expect(body.urgentDeadline).toBe(new Date(2026, 9, 11, 9, 0).toISOString())
  })

  it('omits the deadline when the request is not urgent', () => {
    expect(buildCreatePostRequest(validForm({ urgent: false, deadline: '2026-10-11T09:00' }))).not.toHaveProperty('urgentDeadline')
  })
})
