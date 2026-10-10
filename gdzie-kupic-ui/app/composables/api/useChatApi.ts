import { MESSAGES_PAGE_SIZE, THREADS_PAGE_SIZE } from '~/utils/chat'
import type { ChatMessage, ChatMessagePage, ChatThreadPage, ChatThreadSummary } from '~/utils/chat'

// /api/chat/* — the inbox, the message history and sending, for both roles (the server
// authorises by thread participation).
//
// Behind `public.chatMock` (default in dev) the same calls are answered from an
// in-memory sample conversation (see app/mocks/chat.ts) instead of the backend.
export const useChatApi = () => {
  const api = useApi()
  const useMock = useRuntimeConfig().public.chatMock

  async function mock() {
    const { mockChatFor } = await import('~/mocks/chat')
    const user = useAuthStore().user
    return mockChatFor(user?.role ?? 'Buyer', user?.id ?? 'anonymous')
  }

  return {
    /** One page of the inbox, latest activity first; `cursor` is the `nextCursor` of the previous page. */
    threads: async (cursor: string | null = null, limit: number = THREADS_PAGE_SIZE): Promise<ChatThreadPage> => {
      if (useMock) return (await mock()).listThreads(cursor, limit)

      return api.get<ChatThreadPage>('/api/chat/threads', {
        query: { ...(cursor ? { cursor } : {}), limit },
      })
    },

    /** Rejects with a 404 FetchError for a thread the caller does not take part in. */
    thread: async (threadId: string): Promise<ChatThreadSummary> => {
      if (useMock) return (await mock()).getThread(threadId)
      return api.get<ChatThreadSummary>(`/api/chat/threads/${threadId}`)
    },

    /**
     * Messages in ascending order. Without `before` / `after`: the latest page.
     * `before`: the page right before that message id (older history); `after`: everything newer than it.
     */
    messages: async (
      threadId: string,
      query: { before?: string, after?: string, limit?: number } = {},
    ): Promise<ChatMessagePage> => {
      if (useMock) return (await mock()).getMessages(threadId, query)

      return api.get<ChatMessagePage>(`/api/chat/threads/${threadId}/messages`, {
        query: { limit: MESSAGES_PAGE_SIZE, ...query },
      })
    },

    /**
     * Sends a text message. The endpoint takes `multipart/form-data` (the image part belongs
     * to the attachments ticket). Rejects with 403 `thread_locked`, 400 for empty or too long text.
     */
    send: async (threadId: string, body: string): Promise<ChatMessage> => {
      if (useMock) return (await mock()).sendMessage(threadId, body)

      const form = new FormData()
      form.append('body', body)
      return api.post<ChatMessage>(`/api/chat/threads/${threadId}/messages`, form)
    },

    /** Marks everything in the thread as read for the caller. */
    markRead: async (threadId: string): Promise<void> => {
      if (useMock) {
        ;(await mock()).markRead(threadId)
        return
      }
      await api.post(`/api/chat/threads/${threadId}/read`)
    },

    /** Unread messages over all threads — the navigation badge. */
    unreadCount: async (): Promise<number> => {
      if (useMock) return (await mock()).unreadCount()
      return (await api.get<{ count: number }>('/api/chat/unread-count')).count
    },
  }
}
