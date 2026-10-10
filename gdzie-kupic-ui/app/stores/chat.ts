import {
  MESSAGES_PAGE_SIZE,
  attachmentProblem,
  attachmentProblemFromStatus,
  mergeMessages,
  mergeThreads,
  messageProblem,
  newestMessageId,
  type ChatMessage,
  type ChatThreadSummary,
  type PendingMessage,
} from '~/utils/chat'
import { parseApiError } from '~/utils/apiError'

/** Everything loaded about one thread. */
export interface ChatConversation {
  /** `null` until the thread has been fetched. */
  thread: ChatThreadSummary | null
  /** Confirmed by the server, ascending by time. */
  messages: ChatMessage[]
  /** Older messages than the loaded ones exist. */
  hasMore: boolean
  status: 'pending' | 'success' | 'error' | 'notFound'
  loadingOlder: boolean
  olderFailed: boolean
  /** Sent by the user, not confirmed yet (or failed), oldest first. */
  pending: PendingMessage[]
}

/**
 * `too_large` / `unsupported_type`: the image was refused (nothing is kept, the sender
 * gets text and image back); `locked`: the thread was locked meanwhile (nothing is kept).
 */
export type SendResult = 'sent' | 'failed' | 'locked' | 'invalid' | 'too_large' | 'unsupported_type'

function emptyConversation(): ChatConversation {
  return { thread: null, messages: [], hasMore: false, status: 'pending', loadingOlder: false, olderFailed: false, pending: [] }
}

// Holds the inbox, the open conversations and the unread count so that the inbox, the
// thread view and the navigation badge agree. Cache is bound to the user id: switching
// accounts starts empty. Polling itself lives in the views (`usePolling`); the store
// only offers `refreshThreads()`, `loadUnread()` and `poll()` to call.
export const useChatStore = defineStore('chat', () => {
  const threads = ref<ChatThreadSummary[]>([])
  const nextCursor = ref<string | null>(null)
  const status = ref<'idle' | 'pending' | 'success' | 'error'>('idle')
  const loadingMore = ref(false)
  const loadMoreFailed = ref(false)
  /** Unread messages over all threads; `null` until the first load. */
  const unread = ref<number | null>(null)
  const conversations = ref<Record<string, ChatConversation>>({})
  const owner = ref<string | null>(null)

  let localIds = 0
  // Bumped when the inbox is replaced, so a slower, older response is dropped.
  let generation = 0

  const currentUserId = () => useAuthStore().user?.id ?? null

  function reset() {
    generation++
    threads.value = []
    nextCursor.value = null
    status.value = 'idle'
    loadingMore.value = false
    loadMoreFailed.value = false
    unread.value = null
    conversations.value = {}
    owner.value = null
  }

  /** Nothing of the previous account may stay visible to the next one. */
  function ensureOwner() {
    const id = currentUserId()
    if (owner.value !== id) {
      reset()
      owner.value = id
    }
  }

  function conversationOf(threadId: string): ChatConversation {
    ensureOwner()
    if (!conversations.value[threadId]) conversations.value[threadId] = emptyConversation()
    return conversations.value[threadId]!
  }

  // ─── Inbox ────────────────────────────────────────────────────────────────

  async function fetchFirstPage(): Promise<void> {
    const current = ++generation
    status.value = 'pending'
    loadMoreFailed.value = false
    loadingMore.value = false

    try {
      const page = await useChatApi().threads(null)
      if (current !== generation) return

      threads.value = page.items
      nextCursor.value = page.nextCursor
      status.value = 'success'
    }
    catch {
      if (current === generation) status.value = 'error'
    }
  }

  /** Loads the inbox from its first page (and the unread count). */
  async function loadThreads(): Promise<void> {
    ensureOwner()
    await Promise.all([fetchFirstPage(), loadUnread()])
  }

  /**
   * Brings the inbox up to date: loads it when it has not been (or failed to be) loaded,
   * otherwise re-reads the first page without a loading state and merges it into what is
   * shown (the 30 s refresh). A failure leaves the list as it is.
   */
  async function refreshThreads(): Promise<void> {
    ensureOwner()
    if (status.value === 'idle' || status.value === 'error') return loadThreads()
    if (status.value !== 'success') return

    const current = generation
    try {
      const page = await useChatApi().threads(null)
      if (current !== generation) return

      const hadMore = threads.value.length > page.items.length
      threads.value = mergeThreads(threads.value, page.items)
      if (!hadMore) nextCursor.value = page.nextCursor
    }
    catch {
      // Keeps the current list.
    }
    await loadUnread()
  }

  /** Appends the next page of the inbox; a failure is kept apart so it can be retried. */
  async function loadMoreThreads(): Promise<void> {
    if (status.value !== 'success' || nextCursor.value === null || loadingMore.value) return

    const current = generation
    loadingMore.value = true
    loadMoreFailed.value = false

    try {
      const page = await useChatApi().threads(nextCursor.value)
      if (current !== generation) return

      const known = new Set(threads.value.map(thread => thread.id))
      threads.value = [...threads.value, ...page.items.filter(thread => !known.has(thread.id))]
      nextCursor.value = page.nextCursor
    }
    catch {
      if (current === generation) loadMoreFailed.value = true
    }
    finally {
      if (current === generation) loadingMore.value = false
    }
  }

  async function loadUnread(): Promise<void> {
    ensureOwner()
    const current = generation

    try {
      const count = await useChatApi().unreadCount()
      if (current === generation) unread.value = count
    }
    catch {
      // The badge is optional.
    }
  }

  function setThreadUnread(threadId: string, count: number) {
    const listed = threads.value.find(thread => thread.id === threadId)
    if (listed) listed.unreadCount = count

    const conversation = conversations.value[threadId]
    if (conversation?.thread) conversation.thread.unreadCount = count
  }

  async function markRead(threadId: string): Promise<void> {
    const before = conversations.value[threadId]?.thread?.unreadCount
      ?? threads.value.find(thread => thread.id === threadId)?.unreadCount
      ?? 0

    try {
      await useChatApi().markRead(threadId)
    }
    catch {
      return
    }

    setThreadUnread(threadId, 0)
    if (unread.value !== null) unread.value = Math.max(0, unread.value - before)
    void loadUnread()
  }

  // ─── A conversation ───────────────────────────────────────────────────────

  /**
   * Opens a thread: the summary and the latest messages. What is cached is shown at
   * once and refreshed. Marks the thread read. Never rejects: see `status`.
   */
  async function open(threadId: string): Promise<void> {
    const conversation = conversationOf(threadId)
    if (!conversation.thread && conversation.messages.length === 0) conversation.status = 'pending'

    try {
      const [thread, page] = await Promise.all([
        useChatApi().thread(threadId),
        useChatApi().messages(threadId),
      ])

      const unreadBefore = thread.unreadCount
      conversation.thread = thread
      if (conversation.messages.length === 0) {
        conversation.messages = page.items
        conversation.hasMore = page.hasMore
      }
      else {
        conversation.messages = mergeMessages(conversation.messages, page.items)
      }
      conversation.status = 'success'

      if (unreadBefore > 0) await markRead(threadId)
    }
    catch (err) {
      conversation.status = parseApiError(err).status === 404 ? 'notFound' : 'error'
    }
  }

  /** Loads the page of messages before the oldest loaded one. */
  async function loadOlder(threadId: string): Promise<void> {
    const conversation = conversationOf(threadId)
    const oldest = conversation.messages[0]
    if (!oldest || !conversation.hasMore || conversation.loadingOlder) return

    conversation.loadingOlder = true
    conversation.olderFailed = false

    try {
      const page = await useChatApi().messages(threadId, { before: oldest.id, limit: MESSAGES_PAGE_SIZE })
      conversation.messages = mergeMessages(conversation.messages, page.items)
      conversation.hasMore = page.hasMore
    }
    catch {
      conversation.olderFailed = true
    }
    finally {
      conversation.loadingOlder = false
    }
  }

  /**
   * Asks for messages newer than the newest one loaded (a poll). New messages from the
   * other side are marked read — the thread is open. Returns how many were added.
   */
  async function poll(threadId: string): Promise<number> {
    const conversation = conversationOf(threadId)
    if (conversation.status !== 'success') return 0

    const after = newestMessageId(conversation.messages)
    try {
      const page = after
        ? await useChatApi().messages(threadId, { after })
        : await useChatApi().messages(threadId)

      const known = new Set(conversation.messages.map(message => message.id))
      const added = page.items.filter(message => !known.has(message.id))
      if (added.length === 0) return 0

      conversation.messages = mergeMessages(conversation.messages, added)
      noteLastMessage(threadId, added[added.length - 1]!)
      // The thread is open on screen: what just arrived from the other side is read.
      if (added.some(message => !message.isMine)) await markRead(threadId)
      return added.length
    }
    catch {
      return 0
    }
  }

  /** Keeps the inbox row of a thread in step with its newest message, and moves it to the top. */
  function noteLastMessage(threadId: string, message: ChatMessage) {
    const lastMessage = { preview: (message.body ?? '').slice(0, 100), createdAt: message.createdAt, isMine: message.isMine }

    const conversation = conversations.value[threadId]
    if (conversation?.thread) conversation.thread.lastMessage = lastMessage

    const index = threads.value.findIndex(thread => thread.id === threadId)
    if (index >= 0) {
      const [row] = threads.value.splice(index, 1)
      threads.value.unshift({ ...row!, lastMessage })
    }
  }

  /**
   * Sends `text` and / or an `image`. The message shows at once as pending; the server's copy
   * replaces it. A failure leaves it in the thread as "failed" to retry or discard; `locked`
   * means the thread was locked meanwhile and `too_large` / `unsupported_type` that the
   * image was refused (nothing is kept in these cases).
   */
  async function send(threadId: string, text: string, image: File | null = null): Promise<SendResult> {
    if (messageProblem(text, image !== null)) return 'invalid'
    if (image) {
      const problem = attachmentProblem(image)
      if (problem === 'too_large') return 'too_large'
      if (problem) return problem === 'unsupported_type' ? 'unsupported_type' : 'invalid'
    }

    const conversation = conversationOf(threadId)
    const pending: PendingMessage = {
      localId: `local-${++localIds}`,
      body: text.trim(),
      image,
      previewUrl: image ? URL.createObjectURL(image) : null,
      status: 'sending',
      createdAt: new Date().toISOString(),
    }
    conversation.pending = [...conversation.pending, pending]

    return deliver(threadId, pending.localId)
  }

  /** Removes a pending message and frees its image preview. */
  function dropPending(conversation: ChatConversation, localId: string) {
    const dropped = conversation.pending.find(item => item.localId === localId)
    if (dropped?.previewUrl) URL.revokeObjectURL(dropped.previewUrl)
    conversation.pending = conversation.pending.filter(item => item.localId !== localId)
  }

  async function deliver(threadId: string, localId: string): Promise<SendResult> {
    const conversation = conversationOf(threadId)
    const pending = conversation.pending.find(item => item.localId === localId)
    if (!pending) return 'failed'

    pending.status = 'sending'
    try {
      const message = await useChatApi().send(threadId, pending.body, pending.image)
      dropPending(conversation, localId)
      conversation.messages = mergeMessages(conversation.messages, [message])
      noteLastMessage(threadId, message)
      return 'sent'
    }
    catch (err) {
      const { status: httpStatus } = parseApiError(err)

      if (httpStatus === 403) {
        dropPending(conversation, localId)
        if (conversation.thread) conversation.thread.isLocked = true
        const listed = threads.value.find(thread => thread.id === threadId)
        if (listed) listed.isLocked = true
        return 'locked'
      }

      const refused = attachmentProblemFromStatus(httpStatus)
      if (refused === 'too_large' || refused === 'unsupported_type') {
        dropPending(conversation, localId)
        return refused
      }

      pending.status = 'failed'
      return 'failed'
    }
  }

  /** Sends a failed message again. */
  async function retry(threadId: string, localId: string): Promise<SendResult> {
    return deliver(threadId, localId)
  }

  /** Drops a failed message. */
  function discard(threadId: string, localId: string) {
    const conversation = conversations.value[threadId]
    if (conversation) dropPending(conversation, localId)
  }

  /**
   * Fetches the latest messages again and replaces the loaded copies: the image URLs the
   * server hands out expire, so a picture that no longer loads gets a fresh one this way.
   */
  async function refreshMessages(threadId: string): Promise<void> {
    const conversation = conversationOf(threadId)
    try {
      const page = await useChatApi().messages(threadId)
      conversation.messages = mergeMessages(conversation.messages, page.items)
    }
    catch {
      // The picture keeps its fallback with the retry.
    }
  }

  return {
    threads,
    nextCursor,
    status,
    loadingMore,
    loadMoreFailed,
    unread,
    conversations,
    loadThreads,
    refreshThreads,
    loadMoreThreads,
    loadUnread,
    markRead,
    open,
    loadOlder,
    poll,
    refreshMessages,
    send,
    retry,
    discard,
    reset,
  }
})
