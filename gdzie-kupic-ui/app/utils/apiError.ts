export interface ParsedApiError {
  /** HTTP status, or `null` when no response was received (network error). */
  status: number | null
  /** `detail`/`title` from the backend ProblemDetails, if present. */
  message: string | null
}

interface FetchErrorLike {
  status?: number
  statusCode?: number
  response?: { status?: number }
  data?: { title?: string, detail?: string } | null
}

export function parseApiError(err: unknown): ParsedApiError {
  if (!err || typeof err !== 'object') return { status: null, message: null }

  const e = err as FetchErrorLike
  const status = e.response?.status ?? e.statusCode ?? e.status ?? null
  const message = (e.data?.detail ?? e.data?.title)?.trim() || null

  return { status, message }
}

export interface ApiErrorMessages {
  /** Messages for specific HTTP statuses (e.g. 409 → "name already exists"). */
  byStatus?: Partial<Record<number, string>>
  /** Used for unexpected errors and for 5xx responses without a specific message. */
  fallback: string
  /** Used when the server could not be reached at all. */
  unavailable: string
}

/**
 * Turns a thrown fetch error into a readable message.
 * Server-provided text is only trusted for 4xx responses; 5xx falls back
 * to the generic message so internals are never surfaced to the user.
 */
export function resolveApiError(err: unknown, messages: ApiErrorMessages): string {
  const { status, message } = parseApiError(err)

  if (status === null) return messages.unavailable

  const specific = messages.byStatus?.[status]
  if (specific) return specific

  if (status >= 400 && status < 500 && message) return message

  return messages.fallback
}
