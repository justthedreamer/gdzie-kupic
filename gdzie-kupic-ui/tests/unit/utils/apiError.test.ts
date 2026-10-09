import { describe, it, expect } from 'vitest'
import { parseApiError, resolveApiError } from '~/utils/apiError'

const messages = {
  byStatus: { 409: 'duplicate' },
  fallback: 'fallback',
  unavailable: 'unavailable',
}

describe('parseApiError', () => {
  it('reads the status and ProblemDetails detail from an ofetch error', () => {
    const err = { response: { status: 400 }, data: { title: 'Bad Request', detail: ' Address not found ' } }
    expect(parseApiError(err)).toEqual({ status: 400, message: 'Address not found' })
  })

  it('falls back to title and statusCode', () => {
    expect(parseApiError({ statusCode: 409, data: { title: 'Conflict' } })).toEqual({ status: 409, message: 'Conflict' })
  })

  it('returns a null status when no response was received', () => {
    expect(parseApiError(new TypeError('Failed to fetch'))).toEqual({ status: null, message: null })
    expect(parseApiError(undefined)).toEqual({ status: null, message: null })
  })
})

describe('resolveApiError', () => {
  it('uses the "unavailable" message when the server cannot be reached', () => {
    expect(resolveApiError(new TypeError('Failed to fetch'), messages)).toBe('unavailable')
  })

  it('prefers a status-specific message', () => {
    expect(resolveApiError({ response: { status: 409 }, data: { detail: 'server text' } }, messages)).toBe('duplicate')
  })

  it('shows server-provided text for other 4xx responses', () => {
    expect(resolveApiError({ response: { status: 400 }, data: { detail: 'Name is required' } }, messages)).toBe('Name is required')
  })

  it('never surfaces server text for 5xx responses', () => {
    expect(resolveApiError({ response: { status: 500 }, data: { detail: 'stack trace' } }, messages)).toBe('fallback')
  })

  it('falls back when a 4xx response has no text', () => {
    expect(resolveApiError({ response: { status: 404 } }, messages)).toBe('fallback')
  })
})
