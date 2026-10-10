import type { CreatePostRequest } from '~/composables/api/usePostsApi'

export const RADIUS_PRESETS_KM = [5, 10, 25, 50] as const
export const TITLE_MAX_LENGTH = 120
export const DESCRIPTION_MAX_LENGTH = 2000
export const URGENT_MIN_HOURS = 1
export const URGENT_MAX_HOURS = 72

const HOUR_MS = 60 * 60 * 1000

/** Radius choice of the form: one of the presets, a custom value, or unlimited. Held as strings for the radio group. */
export type RadiusMode = `${typeof RADIUS_PRESETS_KM[number]}` | 'custom' | 'unlimited'

export type PostLocationSource = 'saved' | 'form'

/** Where the post is anchored. `savedId` is set only when the buyer picked one of their saved locations. */
export interface PostLocation extends LocationInputValue {
  source: PostLocationSource
  savedId: string | null
}

export interface PostFormState {
  location: PostLocation | null
  radiusMode: RadiusMode
  customRadius: string
  categoryId: string
  tagId: string
  title: string
  description: string
  urgent: boolean
  /** `datetime-local` value (`YYYY-MM-DDTHH:mm`, local time). */
  deadline: string
}

export function emptyPostForm(): PostFormState {
  return {
    location: null,
    radiusMode: '10',
    customRadius: '',
    categoryId: '',
    tagId: '',
    title: '',
    description: '',
    urgent: false,
    deadline: '',
  }
}

export type PostFormField = 'location' | 'radius' | 'category' | 'tag' | 'title' | 'description' | 'deadline'

export type PostFormErrorCode =
  | 'location_required'
  | 'radius_invalid'
  | 'category_required'
  | 'tag_required'
  | 'tag_invalid'
  | 'title_required'
  | 'title_too_long'
  | 'description_too_long'
  | 'deadline_required'
  | 'deadline_invalid'
  | 'deadline_too_soon'
  | 'deadline_too_far'

export interface PostFormError {
  name: PostFormField
  code: PostFormErrorCode
}

// ─── Radius ─────────────────────────────────────────────────────────────────

/** Custom radius: any number > 0 (a decimal comma is accepted). `null` when the text is not such a number. */
export function parseCustomRadius(text: string): number | null {
  const normalized = text.trim().replace(',', '.')
  if (!normalized) return null

  const value = Number(normalized)
  return Number.isFinite(value) && value > 0 ? value : null
}

export type ResolvedRadius = { valid: true, radiusKm: number | null } | { valid: false }

/** `radiusKm` is `null` for "unlimited" (the API then applies no spatial limit). */
export function resolveRadiusKm(mode: RadiusMode, customRadius: string): ResolvedRadius {
  if (mode === 'unlimited') return { valid: true, radiusKm: null }

  if (mode === 'custom') {
    const custom = parseCustomRadius(customRadius)
    return custom === null ? { valid: false } : { valid: true, radiusKm: custom }
  }

  return { valid: true, radiusKm: Number(mode) }
}

// ─── Urgency ────────────────────────────────────────────────────────────────

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/** Formats a date as a local `datetime-local` value (`YYYY-MM-DDTHH:mm`). */
export function toDateTimeLocal(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/** Parses a `datetime-local` value as local time; `null` when empty or malformed. */
export function parseDeadline(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2})?$/.test(value)) return null

  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

/** Earliest and latest allowed deadline (1–72 h ahead), as `datetime-local` values for the input's min/max. */
export function deadlineBounds(now: Date): { min: string, max: string } {
  return {
    // Round the lower bound up so the picked minute is never already too soon.
    min: toDateTimeLocal(new Date(Math.ceil((now.getTime() + URGENT_MIN_HOURS * HOUR_MS) / 60000) * 60000)),
    max: toDateTimeLocal(new Date(now.getTime() + URGENT_MAX_HOURS * HOUR_MS)),
  }
}

export function validateDeadline(value: string, now: Date): PostFormErrorCode | null {
  if (!value.trim()) return 'deadline_required'

  const deadline = parseDeadline(value)
  if (!deadline) return 'deadline_invalid'

  const ahead = deadline.getTime() - now.getTime()
  if (ahead < URGENT_MIN_HOURS * HOUR_MS) return 'deadline_too_soon'
  if (ahead > URGENT_MAX_HOURS * HOUR_MS) return 'deadline_too_far'

  return null
}

// ─── Catalogue ──────────────────────────────────────────────────────────────

export function enabledCategories(categories: CatalogueCategory[]): CatalogueCategory[] {
  return categories.filter(category => !category.isDisabled)
}

/** Enabled tags of an enabled category (nothing for a disabled or unknown category). */
export function enabledTags(categories: CatalogueCategory[], categoryId: string): CatalogueTag[] {
  const category = enabledCategories(categories).find(candidate => candidate.id === categoryId)
  return category ? category.tags.filter(tag => !tag.isDisabled) : []
}

// ─── Validation & request ───────────────────────────────────────────────────

/** Mirrors the API contract of `POST /api/posts`. An empty list means the form can be submitted. */
export function validatePostForm(state: PostFormState, categories: CatalogueCategory[], now: Date): PostFormError[] {
  const errors: PostFormError[] = []

  if (!state.location) errors.push({ name: 'location', code: 'location_required' })

  if (!resolveRadiusKm(state.radiusMode, state.customRadius).valid) {
    errors.push({ name: 'radius', code: 'radius_invalid' })
  }

  if (!state.categoryId) {
    errors.push({ name: 'category', code: 'category_required' })
  }
  else if (!state.tagId) {
    errors.push({ name: 'tag', code: 'tag_required' })
  }
  else if (!enabledTags(categories, state.categoryId).some(tag => tag.id === state.tagId)) {
    errors.push({ name: 'tag', code: 'tag_invalid' })
  }

  const title = state.title.trim()
  if (!title) errors.push({ name: 'title', code: 'title_required' })
  else if (title.length > TITLE_MAX_LENGTH) errors.push({ name: 'title', code: 'title_too_long' })

  if (state.description.trim().length > DESCRIPTION_MAX_LENGTH) {
    errors.push({ name: 'description', code: 'description_too_long' })
  }

  if (state.urgent) {
    const code = validateDeadline(state.deadline, now)
    if (code) errors.push({ name: 'deadline', code })
  }

  return errors
}

/** Builds the request body; call only for a valid form (see `validatePostForm`). */
export function buildCreatePostRequest(state: PostFormState): CreatePostRequest {
  const radius = resolveRadiusKm(state.radiusMode, state.customRadius)
  const deadline = state.urgent ? parseDeadline(state.deadline) : null
  const description = state.description.trim()

  return {
    latitude: state.location!.latitude,
    longitude: state.location!.longitude,
    radiusKm: radius.valid ? radius.radiusKm : null,
    categoryId: state.categoryId,
    tagId: state.tagId,
    title: state.title.trim(),
    ...(description ? { description } : {}),
    ...(deadline ? { urgentDeadline: deadline.toISOString() } : {}),
  }
}
