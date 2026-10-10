// Rules for the optional first name; they mirror the server (`FirstName` in the
// service's domain), which is the authority. Empty means "no name".

export const FIRST_NAME_MAX_LENGTH = 50

export type FirstNameProblem = 'too_long' | 'invalid'

/** Trims and collapses whitespace runs, as the server does. */
export function normalizeFirstName(input: string): string {
  return input.trim().replace(/\s+/g, ' ')
}

const ALLOWED = /^\p{L}[\p{L}\p{M} '’-]*$/u

/** What is wrong with the (normalized) name, or `null` when it is fine or empty. */
export function firstNameProblem(input: string): FirstNameProblem | null {
  const value = normalizeFirstName(input)

  if (value.length === 0) return null
  if (value.length > FIRST_NAME_MAX_LENGTH) return 'too_long'

  return ALLOWED.test(value) ? null : 'invalid'
}
