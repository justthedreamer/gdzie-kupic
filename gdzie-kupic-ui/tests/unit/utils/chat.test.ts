import { describe, it, expect } from 'vitest'
import {
  ATTACHMENT_ACCEPT,
  MAX_ATTACHMENT_BYTES,
  MAX_MESSAGE_LENGTH,
  attachmentProblem,
  attachmentProblemFromStatus,
  formatMessageTime,
  isNearEnd,
  mergeMessages,
  mergeThreads,
  messageProblem,
  newestMessageId,
  threadPath,
  type ChatMessage,
  type ChatThreadSummary,
} from '~/utils/chat'

const message = (id: string, minute: number, body = id): ChatMessage => ({
  id,
  threadId: 't1',
  senderId: 'u1',
  isMine: false,
  body,
  attachmentUrl: null,
  createdAt: new Date(Date.UTC(2026, 0, 1, 12, minute)).toISOString(),
})

const thread = (id: string, unreadCount = 0): ChatThreadSummary => ({
  id,
  post: { id: `p-${id}`, title: id, status: 'Active' },
  counterpart: { id: 'c', displayName: 'Shop' },
  lastMessage: null,
  unreadCount,
  isLocked: false,
  createdAt: '2026-01-01T12:00:00Z',
})

describe('threadPath', () => {
  it('points at the thread view', () => {
    expect(threadPath('abc')).toBe('/chat/abc')
  })
})

describe('messageProblem', () => {
  it('rejects empty and whitespace-only text', () => {
    expect(messageProblem('')).toBe('empty')
    expect(messageProblem('  \n ')).toBe('empty')
  })

  it('accepts text up to the limit, ignoring surrounding whitespace', () => {
    expect(messageProblem('hej')).toBeNull()
    expect(messageProblem(` ${'a'.repeat(MAX_MESSAGE_LENGTH)} `)).toBeNull()
  })

  it('rejects text over the limit', () => {
    expect(messageProblem('a'.repeat(MAX_MESSAGE_LENGTH + 1))).toBe('too_long')
  })

  it('lets an image stand without text, but not text that is too long', () => {
    expect(messageProblem('', true)).toBeNull()
    expect(messageProblem('   ', true)).toBeNull()
    expect(messageProblem('a'.repeat(MAX_MESSAGE_LENGTH + 1), true)).toBe('too_long')
  })
})

describe('attachmentProblem', () => {
  const file = (type: string, size = 1000) => ({ type, size })

  it.each(['image/jpeg', 'image/png', 'image/webp'])('accepts %s', (type) => {
    expect(attachmentProblem(file(type))).toBeNull()
  })

  it.each(['image/gif', 'image/svg+xml', 'application/pdf', 'text/plain', ''])('rejects the type "%s"', (type) => {
    expect(attachmentProblem(file(type))).toBe('unsupported_type')
  })

  it('accepts a file up to the size limit and rejects a larger one', () => {
    expect(attachmentProblem(file('image/png', MAX_ATTACHMENT_BYTES))).toBeNull()
    expect(attachmentProblem(file('image/png', MAX_ATTACHMENT_BYTES + 1))).toBe('too_large')
  })

  it('rejects an empty file', () => {
    expect(attachmentProblem(file('image/png', 0))).toBe('empty_file')
  })

  it('checks the type before the size', () => {
    expect(attachmentProblem(file('image/gif', MAX_ATTACHMENT_BYTES + 1))).toBe('unsupported_type')
  })

  it('has an accept value with exactly the allowed types', () => {
    expect(ATTACHMENT_ACCEPT).toBe('image/jpeg,image/png,image/webp')
  })
})

describe('attachmentProblemFromStatus', () => {
  it('maps the server answers 413 and 415', () => {
    expect(attachmentProblemFromStatus(413)).toBe('too_large')
    expect(attachmentProblemFromStatus(415)).toBe('unsupported_type')
  })

  it('knows nothing about other answers', () => {
    expect(attachmentProblemFromStatus(400)).toBeNull()
    expect(attachmentProblemFromStatus(500)).toBeNull()
    expect(attachmentProblemFromStatus(null)).toBeNull()
  })
})

describe('mergeMessages', () => {
  it('keeps the result ascending by time', () => {
    const merged = mergeMessages([message('c', 3), message('d', 4)], [message('a', 1), message('b', 2)])

    expect(merged.map(m => m.id)).toEqual(['a', 'b', 'c', 'd'])
  })

  it('replaces a message that is already there instead of duplicating it', () => {
    const merged = mergeMessages([message('a', 1, 'old')], [message('a', 1, 'new'), message('b', 2)])

    expect(merged).toHaveLength(2)
    expect(merged[0]!.body).toBe('new')
  })

  it('keeps the existing order for messages sent in the same instant', () => {
    const merged = mergeMessages([message('b', 1), message('a', 1)], [message('c', 1)])

    expect(merged.map(m => m.id)).toEqual(['b', 'a', 'c'])
  })

  it('does not change the input', () => {
    const existing = [message('a', 1)]
    mergeMessages(existing, [message('b', 2)])

    expect(existing).toHaveLength(1)
  })
})

describe('mergeThreads', () => {
  it('puts the fresh first page first and keeps older pages below it', () => {
    const merged = mergeThreads([thread('a'), thread('b'), thread('c')], [thread('c', 2), thread('new')])

    expect(merged.map(t => t.id)).toEqual(['c', 'new', 'a', 'b'])
    expect(merged[0]!.unreadCount).toBe(2)
  })
})

describe('newestMessageId', () => {
  it('is the id of the last message, or null', () => {
    expect(newestMessageId([message('a', 1), message('b', 2)])).toBe('b')
    expect(newestMessageId([])).toBeNull()
  })
})

describe('isNearEnd', () => {
  it('is true within the threshold of the end', () => {
    expect(isNearEnd({ scrollTop: 880, scrollHeight: 1500, clientHeight: 500 })).toBe(true)
    expect(isNearEnd({ scrollTop: 1000, scrollHeight: 1500, clientHeight: 500 })).toBe(true)
  })

  it('is false when scrolled up', () => {
    expect(isNearEnd({ scrollTop: 100, scrollHeight: 1500, clientHeight: 500 })).toBe(false)
  })
})

describe('formatMessageTime', () => {
  const now = new Date(2026, 0, 10, 15, 0)

  it('shows only the hour for a message from today', () => {
    expect(formatMessageTime(new Date(2026, 0, 10, 9, 5).toISOString(), 'en-GB', now)).toBe('09:05')
  })

  it('adds the date for another day', () => {
    const text = formatMessageTime(new Date(2026, 0, 8, 9, 5).toISOString(), 'en-GB', now)

    expect(text).toContain('Jan')
    expect(text).toContain('09:05')
  })
})
