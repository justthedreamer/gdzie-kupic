import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useChatStore } from '~/stores/chat'
import { MockChat } from '~/mocks/chat'
import { postIdInPath, threadIdInPath } from '~/utils/chat'

const api = vi.hoisted(() => ({
  threads: vi.fn(),
  thread: vi.fn(),
  messages: vi.fn(),
  send: vi.fn(),
  markRead: vi.fn(),
  unreadCount: vi.fn(),
}))

mockNuxtImport('useChatApi', () => () => api)

let server: MockChat

const incoming = (threadId: string, body: string) =>
  sessionStorage.setItem('gk:mock-chat-incoming', JSON.stringify([{ threadId, body }]))

// What the shell and the thread page do with the real-time events goes through
// `refreshInbox()` and `refreshThread()` (and `poll()` for a message in the open thread).
describe('chat store - real-time refresh', () => {
  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
    server = new MockChat('Buyer', 'b1')
    api.threads.mockReset().mockImplementation(async (cursor: string | null, limit?: number) => server.listThreads(cursor, limit))
    api.thread.mockReset().mockImplementation(async (id: string) => server.getThread(id))
    api.messages.mockReset().mockImplementation(async (id: string, query?: object) => server.getMessages(id, query))
    api.send.mockReset()
    api.markRead.mockReset().mockImplementation(async (id: string) => server.markRead(id))
    api.unreadCount.mockReset().mockImplementation(async () => server.unreadCount())
    useAuthStore().setAuth('t', { id: 'b1', email: 'b@test', role: 'Buyer' })
  })

  describe('refreshInbox', () => {
    it('only reads the unread count while the inbox was never loaded', async () => {
      const store = useChatStore()
      incoming('thread-closed', 'Nowa wiadomość')

      await store.refreshInbox()

      expect(store.unread).toBe(3)
      expect(store.status).toBe('idle')
      expect(api.threads).not.toHaveBeenCalled()
    })

    it('updates the rows and the unread count of a loaded inbox', async () => {
      const store = useChatStore()
      await store.loadThreads()
      expect(store.unread).toBe(2)

      incoming('thread-closed', 'Wysyłamy jeszcze dziś.')
      await store.refreshInbox()

      expect(store.unread).toBe(3)
      const row = store.threads.find(thread => thread.id === 'thread-closed')!
      expect(row.unreadCount).toBe(1)
      expect(row.lastMessage?.preview).toBe('Wysyłamy jeszcze dziś.')
      expect(store.threads[0]!.id).toBe('thread-closed') // the newest activity first
    })

    it('adds a thread created meanwhile', async () => {
      const store = useChatStore()
      await store.loadThreads()
      expect(store.threads).toHaveLength(3)

      incoming('thread-feed-2', 'Dzień dobry, mamy ten interfejs.')
      await store.refreshInbox()

      expect(store.threads).toHaveLength(4)
      expect(store.threads.find(thread => thread.id === 'thread-feed-2')?.unreadCount).toBe(1)
    })

    it('shows a thread that got locked', async () => {
      const store = useChatStore()
      await store.loadThreads()
      expect(store.threads.find(thread => thread.id === 'thread-closed')?.isLocked).toBe(false)

      sessionStorage.setItem('gk:mock-chat-lock', JSON.stringify(['thread-closed']))
      await store.refreshInbox()

      expect(store.threads.find(thread => thread.id === 'thread-closed')?.isLocked).toBe(true)
    })
  })

  describe('refreshThread', () => {
    it('updates the locked state of an open thread and keeps its unread count', async () => {
      const store = useChatStore()
      await store.open('thread-feed-4')
      expect(store.conversations['thread-feed-4']!.thread!.isLocked).toBe(false)

      sessionStorage.setItem('gk:mock-chat-lock', JSON.stringify(['thread-feed-4']))
      // The server's unread count may still be ahead of what the open thread has marked read.
      store.conversations['thread-feed-4']!.thread!.unreadCount = 0
      await store.refreshThread('thread-feed-4')

      const thread = store.conversations['thread-feed-4']!.thread!
      expect(thread.isLocked).toBe(true)
      expect(thread.unreadCount).toBe(0)
    })

    it('ignores a thread that is not loaded', async () => {
      const store = useChatStore()

      await store.refreshThread('thread-closed')

      expect(api.thread).not.toHaveBeenCalled()
    })

    it('keeps the summary when the request fails', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      api.thread.mockRejectedValueOnce(new Error('boom'))

      await store.refreshThread('thread-closed')

      expect(store.conversations['thread-closed']!.thread).not.toBeNull()
      expect(store.conversations['thread-closed']!.status).toBe('success')
    })
  })

  describe('a message in the open thread', () => {
    it('is fetched and marked read by poll()', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      const before = store.conversations['thread-closed']!.messages.length

      incoming('thread-closed', 'Wysyłamy jeszcze dziś.')
      const added = await store.poll('thread-closed')

      expect(added).toBe(1)
      expect(store.conversations['thread-closed']!.messages).toHaveLength(before + 1)
      expect(api.markRead).toHaveBeenCalledWith('thread-closed')
      expect(store.unread).toBe(2) // only the other thread's unread messages remain
    })
  })
})

describe('chat paths', () => {
  it('finds the open thread and the open request', () => {
    expect(threadIdInPath('/chat/thread-1')).toBe('thread-1')
    expect(threadIdInPath('/chat')).toBeNull()
    expect(threadIdInPath('/home')).toBeNull()

    expect(postIdInPath('/requests/p1')).toBe('p1')
    expect(postIdInPath('/feed/feed-4')).toBe('feed-4')
    expect(postIdInPath('/chat/p1')).toBeNull()
    expect(postIdInPath('/requests')).toBeNull()
  })
})
