import type { PostStatus } from '~/composables/api/usePostsApi'

// Data model and helpers of the chat — `ThreadSummary` and `Message` of the Phase 5
// API contract (planning/phase-5-merchant-response-chat.md). `useChatApi` supplies
// them, from the backend or (behind `chatMock`) from mock data.

export interface ChatThreadSummary {
  id: string
  post: { id: string, title: string, status: PostStatus }
  /** The shop name for a buyer, the buyer's first name for a merchant. */
  counterpart: { id: string, displayName: string }
  lastMessage: { preview: string, createdAt: string, isMine: boolean } | null
  unreadCount: number
  /** One of the participants is banned: nobody can write any more. */
  isLocked: boolean
  createdAt: string
}

export interface ChatThreadPage {
  items: ChatThreadSummary[]
  nextCursor: string | null
}

export interface ChatMessage {
  id: string
  threadId: string
  senderId: string
  isMine: boolean
  body: string | null
  /** Short-lived URL of an attached image (chat image tickets). */
  attachmentUrl: string | null
  /** ISO timestamp. */
  createdAt: string
}

/** Ascending by time; `hasMore` = there are older messages than the page. */
export interface ChatMessagePage {
  items: ChatMessage[]
  hasMore: boolean
}

/** A message the user sent that the server has not confirmed (yet). */
export interface PendingMessage {
  localId: string
  body: string
  status: 'sending' | 'failed'
  /** ISO timestamp of the click. */
  createdAt: string
}

export const THREADS_PAGE_SIZE = 20
export const MESSAGES_PAGE_SIZE = 30
export const MAX_MESSAGE_LENGTH = 2000

/** How often an open thread asks for new messages (until real-time arrives in Phase 6). */
export const POLL_INTERVAL_MS = 4000
/** How often the inbox and the navigation badge refresh. */
export const INBOX_REFRESH_MS = 30_000

/** The thread view of a conversation, for both roles. */
export const threadPath = (threadId: string): string => `/chat/${threadId}`
export const CHAT_PATH = '/chat'

export type MessageProblem = 'empty' | 'too_long'

/** Why `text` cannot be sent, or `null` when it can. Surrounding whitespace does not count. */
export function messageProblem(text: string): MessageProblem | null {
  const trimmed = text.trim()
  if (trimmed.length === 0) return 'empty'
  if (trimmed.length > MAX_MESSAGE_LENGTH) return 'too_long'
  return null
}

/**
 * Adds `incoming` to `existing`: a message that is already there (same id) is replaced
 * by the newer copy, the result is ascending by time (ties keep the existing order).
 */
export function mergeMessages(existing: ChatMessage[], incoming: ChatMessage[]): ChatMessage[] {
  const byId = new Map(existing.map(message => [message.id, message]))
  for (const message of incoming) byId.set(message.id, message)

  return [...byId.values()]
    .map((message, index) => ({ message, index }))
    .sort((a, b) => Date.parse(a.message.createdAt) - Date.parse(b.message.createdAt) || a.index - b.index)
    .map(({ message }) => message)
}

/**
 * The first page of the inbox, merged into what is loaded: refreshed and new threads
 * come first (in the server's order), threads loaded from later pages stay below them.
 */
export function mergeThreads(loaded: ChatThreadSummary[], firstPage: ChatThreadSummary[]): ChatThreadSummary[] {
  const fresh = new Set(firstPage.map(thread => thread.id))
  return [...firstPage, ...loaded.filter(thread => !fresh.has(thread.id))]
}

/** The id to ask `after=` for new messages: the newest one the server confirmed. */
export function newestMessageId(messages: ChatMessage[]): string | null {
  return messages.length ? messages[messages.length - 1]!.id : null
}

/** Is a scroll container within `threshold` px of its end? */
export function isNearEnd(position: { scrollTop: number, scrollHeight: number, clientHeight: number }, threshold = 120): boolean {
  return position.scrollHeight - position.scrollTop - position.clientHeight <= threshold
}

/** Time of a message: just the hour today, the date and the hour on another day. */
export function formatMessageTime(iso: string, locale: string, now: Date = new Date()): string {
  const date = new Date(iso)
  const time = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(date)
  if (date.toDateString() === now.toDateString()) return time

  return `${new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short' }).format(date)}, ${time}`
}
