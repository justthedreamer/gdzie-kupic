import { describe, it, expect } from 'vitest'
import {
  MAX_MESSAGE_LENGTH,
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
