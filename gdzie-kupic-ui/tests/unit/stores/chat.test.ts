import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { useChatStore } from '~/stores/chat'
import { MockChat } from '~/mocks/chat'
import { MAX_ATTACHMENT_BYTES, MAX_MESSAGE_LENGTH, MESSAGES_PAGE_SIZE } from '~/utils/chat'

// The store talks to the mock chat "server", which behaves like the real endpoints.
let server: MockChat

const api = vi.hoisted(() => ({
  threads: vi.fn(),
  thread: vi.fn(),
  messages: vi.fn(),
  send: vi.fn(),
  markRead: vi.fn(),
  unreadCount: vi.fn(),
}))

mockNuxtImport('useChatApi', () => () => api)

function wire() {
  api.threads.mockReset().mockImplementation(async (cursor: string | null, limit?: number) => server.listThreads(cursor, limit))
  api.thread.mockReset().mockImplementation(async (id: string) => server.getThread(id))
  api.messages.mockReset().mockImplementation(async (id: string, query?: object) => server.getMessages(id, query))
  api.send.mockReset().mockImplementation(async (id: string, body: string, image?: File | null) => server.sendMessage(id, body, image))
  api.markRead.mockReset().mockImplementation(async (id: string) => server.markRead(id))
  api.unreadCount.mockReset().mockImplementation(async () => server.unreadCount())
}

describe('chat store', () => {
  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
    server = new MockChat('Buyer', 'b1')
    wire()
    useAuthStore().setAuth('t', { id: 'b1', email: 'b@test', role: 'Buyer' })
  })

  describe('inbox', () => {
    it('loads the first page and the unread count', async () => {
      const store = useChatStore()

      await store.loadThreads()

      expect(store.status).toBe('success')
      expect(store.threads.map(thread => thread.id).sort()).toEqual(['thread-closed', 'thread-feed-4', 'thread-locked'])
      expect(store.unread).toBe(2)
    })

    it('reports a failed load and recovers on refresh', async () => {
      api.threads.mockRejectedValueOnce(new Error('boom'))
      const store = useChatStore()

      await store.loadThreads()
      expect(store.status).toBe('error')

      await store.refreshThreads()
      expect(store.status).toBe('success')
      expect(store.threads).toHaveLength(3)
    })

    it('pages with the cursor and does not duplicate threads', async () => {
      api.threads.mockImplementation(async (cursor: string | null) => server.listThreads(cursor, 2))
      const store = useChatStore()

      await store.loadThreads()
      expect(store.threads).toHaveLength(2)
      expect(store.nextCursor).not.toBeNull()

      await store.loadMoreThreads()
      expect(store.threads).toHaveLength(3)
      expect(store.nextCursor).toBeNull()
    })

    it('keeps a failed "load more" apart so it can be retried', async () => {
      api.threads.mockImplementation(async (cursor: string | null) => server.listThreads(cursor, 2))
      const store = useChatStore()
      await store.loadThreads()

      api.threads.mockRejectedValueOnce(new Error('boom'))
      await store.loadMoreThreads()
      expect(store.loadMoreFailed).toBe(true)
      expect(store.status).toBe('success')
      expect(store.threads).toHaveLength(2)

      await store.loadMoreThreads()
      expect(store.loadMoreFailed).toBe(false)
      expect(store.threads).toHaveLength(3)
    })

    it('refresh brings a new incoming message to the top without a loading state', async () => {
      const store = useChatStore()
      await store.loadThreads()

      sessionStorage.setItem('gk:mock-chat-incoming', JSON.stringify([{ threadId: 'thread-locked', body: 'Czwarty kabel już jest' }]))
      await store.refreshThreads()

      expect(store.status).toBe('success')
      expect(store.threads[0]!.id).toBe('thread-locked')
      expect(store.threads[0]!.lastMessage?.preview).toBe('Czwarty kabel już jest')
      expect(store.unread).toBe(3)
    })

    it('does not show the previous account\'s conversations to the next one', async () => {
      const store = useChatStore()
      await store.loadThreads()
      await store.open('thread-feed-4')

      useAuthStore().setAuth('t', { id: 'b2', email: 'b2@test', role: 'Buyer' })
      api.threads.mockRejectedValue(new Error('boom'))
      await store.refreshThreads()

      expect(store.threads).toEqual([])
      expect(store.status).toBe('error')
      expect(store.conversations).toEqual({})
    })
  })

  describe('opening a thread', () => {
    it('loads the summary and the latest page, and marks the thread read', async () => {
      const store = useChatStore()
      await store.loadThreads()

      await store.open('thread-feed-4')
      const conversation = store.conversations['thread-feed-4']!

      expect(conversation.status).toBe('success')
      expect(conversation.messages).toHaveLength(MESSAGES_PAGE_SIZE)
      expect(conversation.hasMore).toBe(true)
      expect(api.markRead).toHaveBeenCalledWith('thread-feed-4')
      expect(conversation.thread?.unreadCount).toBe(0)
      expect(store.threads.find(thread => thread.id === 'thread-feed-4')?.unreadCount).toBe(0)
      expect(store.unread).toBe(0)
    })

    it('does not call mark-read for a thread without unread messages', async () => {
      const store = useChatStore()

      await store.open('thread-closed')

      expect(api.markRead).not.toHaveBeenCalled()
    })

    it('reports a thread that does not exist', async () => {
      const store = useChatStore()

      await store.open('does-not-exist')

      expect(store.conversations['does-not-exist']!.status).toBe('notFound')
    })

    it('reports other load failures, and loads again on retry', async () => {
      api.thread.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusCode: 500 }))
      const store = useChatStore()

      await store.open('thread-closed')
      expect(store.conversations['thread-closed']!.status).toBe('error')

      await store.open('thread-closed')
      expect(store.conversations['thread-closed']!.status).toBe('success')
    })

    it('loads older pages before the oldest message until the history ends', async () => {
      const store = useChatStore()
      await store.open('thread-feed-4')
      const conversation = store.conversations['thread-feed-4']!
      const newest = conversation.messages.at(-1)!.id

      await store.loadOlder('thread-feed-4')
      expect(conversation.messages).toHaveLength(47)
      expect(conversation.hasMore).toBe(false)
      expect(conversation.messages.at(-1)!.id).toBe(newest)
      expect(new Set(conversation.messages.map(message => message.id)).size).toBe(47)

      api.messages.mockClear()
      await store.loadOlder('thread-feed-4')
      expect(api.messages).not.toHaveBeenCalled()
    })

    it('keeps a failed older page apart so it can be retried', async () => {
      const store = useChatStore()
      await store.open('thread-feed-4')

      api.messages.mockRejectedValueOnce(new Error('boom'))
      await store.loadOlder('thread-feed-4')
      const conversation = store.conversations['thread-feed-4']!
      expect(conversation.olderFailed).toBe(true)
      expect(conversation.messages).toHaveLength(MESSAGES_PAGE_SIZE)

      await store.loadOlder('thread-feed-4')
      expect(conversation.olderFailed).toBe(false)
      expect(conversation.messages).toHaveLength(47)
    })
  })

  describe('polling', () => {
    it('adds only the new messages, marks them read and moves the thread up', async () => {
      const store = useChatStore()
      await store.loadThreads()
      await store.open('thread-closed')

      sessionStorage.setItem('gk:mock-chat-incoming', JSON.stringify([{ threadId: 'thread-closed', body: 'Jest jeszcze jedna sztuka' }]))
      const added = await store.poll('thread-closed')

      const conversation = store.conversations['thread-closed']!
      expect(added).toBe(1)
      expect(conversation.messages.at(-1)!.body).toBe('Jest jeszcze jedna sztuka')
      expect(api.markRead).toHaveBeenCalledWith('thread-closed')
      expect(conversation.thread?.unreadCount).toBe(0)
      expect(store.threads[0]!.id).toBe('thread-closed')
      expect(store.threads[0]!.lastMessage?.preview).toBe('Jest jeszcze jedna sztuka')
    })

    it('reports nothing new when nothing arrived, and survives a failing poll', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      expect(await store.poll('thread-closed')).toBe(0)

      api.messages.mockRejectedValueOnce(new Error('boom'))
      expect(await store.poll('thread-closed')).toBe(0)
      expect(store.conversations['thread-closed']!.status).toBe('success')
    })

    it('does nothing for a thread that is not loaded', async () => {
      const store = useChatStore()

      expect(await store.poll('thread-closed')).toBe(0)
      expect(api.messages).not.toHaveBeenCalled()
    })
  })

  describe('sending', () => {
    it('shows the message as pending, then replaces it with the confirmed one', async () => {
      const store = useChatStore()
      await store.loadThreads()
      await store.open('thread-closed')

      const sending = store.send('thread-closed', '  Dziękuję!  ')
      const conversation = store.conversations['thread-closed']!
      expect(conversation.pending).toHaveLength(1)
      expect(conversation.pending[0]).toMatchObject({ body: 'Dziękuję!', status: 'sending' })

      expect(await sending).toBe('sent')
      expect(conversation.pending).toEqual([])
      expect(conversation.messages.at(-1)).toMatchObject({ body: 'Dziękuję!', isMine: true })
      expect(store.threads[0]).toMatchObject({ id: 'thread-closed', lastMessage: { preview: 'Dziękuję!', isMine: true } })
    })

    it('does not send empty or too long text', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      expect(await store.send('thread-closed', '   ')).toBe('invalid')
      expect(await store.send('thread-closed', 'a'.repeat(MAX_MESSAGE_LENGTH + 1))).toBe('invalid')

      expect(api.send).not.toHaveBeenCalled()
      expect(store.conversations['thread-closed']!.pending).toEqual([])
    })

    it('keeps a failed message to retry, and sends it once on retry', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      api.send.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusCode: 500 }))
      expect(await store.send('thread-closed', 'Halo?')).toBe('failed')

      const conversation = store.conversations['thread-closed']!
      expect(conversation.pending).toHaveLength(1)
      expect(conversation.pending[0]!.status).toBe('failed')

      expect(await store.retry('thread-closed', conversation.pending[0]!.localId)).toBe('sent')
      expect(conversation.pending).toEqual([])
      expect(conversation.messages.filter(message => message.body === 'Halo?')).toHaveLength(1)
    })

    it('discards a failed message', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      api.send.mockRejectedValueOnce(new Error('boom'))
      await store.send('thread-closed', 'Halo?')
      const conversation = store.conversations['thread-closed']!

      store.discard('thread-closed', conversation.pending[0]!.localId)

      expect(conversation.pending).toEqual([])
    })

    it('locks the thread when the server says it is locked, keeping no pending message', async () => {
      const store = useChatStore()
      await store.loadThreads()
      await store.open('thread-feed-4')
      api.send.mockRejectedValueOnce(Object.assign(new Error('thread_locked'), { statusCode: 403, data: { code: 'thread_locked' } }))

      expect(await store.send('thread-feed-4', 'Czy dalej aktualne?')).toBe('locked')

      const conversation = store.conversations['thread-feed-4']!
      expect(conversation.pending).toEqual([])
      expect(conversation.thread?.isLocked).toBe(true)
      expect(store.threads.find(thread => thread.id === 'thread-feed-4')?.isLocked).toBe(true)
    })

    it('does not send into a thread that is locked on the server', async () => {
      const store = useChatStore()
      await store.open('thread-locked')

      expect(await store.send('thread-locked', 'Halo')).toBe('locked')
      expect(store.conversations['thread-locked']!.messages.at(-1)!.body).not.toBe('Halo')
    })
  })

  describe('sending an image', () => {
    const png = () => new File(['x'], 'a.png', { type: 'image/png' })
    const revoke = vi.fn()

    beforeEach(() => {
      revoke.mockReset()
      URL.createObjectURL = vi.fn(() => 'blob:preview')
      URL.revokeObjectURL = revoke
    })

    it('sends an image without text, showing a preview while it is pending', async () => {
      const store = useChatStore()
      await store.loadThreads()
      await store.open('thread-closed')
      const file = png()

      const sending = store.send('thread-closed', '', file)
      const conversation = store.conversations['thread-closed']!
      expect(conversation.pending[0]).toMatchObject({ body: '', image: file, previewUrl: 'blob:preview', status: 'sending' })

      expect(await sending).toBe('sent')
      expect(api.send).toHaveBeenCalledWith('thread-closed', '', file)
      expect(conversation.pending).toEqual([])
      expect(revoke).toHaveBeenCalledWith('blob:preview')
      expect(conversation.messages.at(-1)).toMatchObject({ body: null, isMine: true })
      expect(conversation.messages.at(-1)!.attachmentUrl).not.toBeNull()
      expect(store.threads[0]).toMatchObject({ id: 'thread-closed', lastMessage: { preview: '' } })
    })

    it('sends text together with the image', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      expect(await store.send('thread-closed', 'Takie?', png())).toBe('sent')

      expect(store.conversations['thread-closed']!.messages.at(-1)).toMatchObject({ body: 'Takie?' })
    })

    it('still needs a text or an image', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      expect(await store.send('thread-closed', '  ', null)).toBe('invalid')
      expect(api.send).not.toHaveBeenCalled()
    })

    it('does not upload a file that breaks the rules', async () => {
      const store = useChatStore()
      await store.open('thread-closed')

      expect(await store.send('thread-closed', '', new File(['x'], 'a.gif', { type: 'image/gif' }))).toBe('unsupported_type')
      const big = new File(['x'], 'big.png', { type: 'image/png' })
      Object.defineProperty(big, 'size', { value: MAX_ATTACHMENT_BYTES + 1 })
      expect(await store.send('thread-closed', '', big)).toBe('too_large')

      expect(api.send).not.toHaveBeenCalled()
      expect(store.conversations['thread-closed']!.pending).toEqual([])
    })

    it.each([
      [413, 'too_large'],
      [415, 'unsupported_type'],
    ])('hands the image back when the server answers %i, keeping no pending message', async (statusCode, result) => {
      const store = useChatStore()
      await store.open('thread-closed')
      api.send.mockRejectedValueOnce(Object.assign(new Error('refused'), { statusCode }))

      expect(await store.send('thread-closed', 'Takie?', png())).toBe(result)

      expect(store.conversations['thread-closed']!.pending).toEqual([])
      expect(revoke).toHaveBeenCalledWith('blob:preview')
    })

    it('keeps a failed image message and sends the same file again on retry', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      const file = png()
      api.send.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusCode: 500 }))

      expect(await store.send('thread-closed', '', file)).toBe('failed')
      const pending = store.conversations['thread-closed']!.pending[0]!
      expect(pending.status).toBe('failed')
      expect(revoke).not.toHaveBeenCalled()

      expect(await store.retry('thread-closed', pending.localId)).toBe('sent')
      expect(api.send).toHaveBeenLastCalledWith('thread-closed', '', file)
    })

    it('frees the preview of a discarded message', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      api.send.mockRejectedValueOnce(new Error('boom'))
      await store.send('thread-closed', '', png())

      store.discard('thread-closed', store.conversations['thread-closed']!.pending[0]!.localId)

      expect(revoke).toHaveBeenCalledWith('blob:preview')
    })
  })

  describe('refreshing the pictures', () => {
    it('replaces the loaded messages with the server\'s fresh copies', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      const newest = store.conversations['thread-closed']!.messages.at(-1)!
      api.messages.mockResolvedValueOnce({ items: [{ ...newest, attachmentUrl: 'https://files.test/fresh.png' }], hasMore: true })

      await store.refreshMessages('thread-closed')

      const messages = store.conversations['thread-closed']!.messages
      expect(messages.at(-1)!.attachmentUrl).toBe('https://files.test/fresh.png')
      expect(messages.filter(message => message.id === newest.id)).toHaveLength(1)
    })

    it('keeps what is loaded when the refresh fails', async () => {
      const store = useChatStore()
      await store.open('thread-closed')
      const before = store.conversations['thread-closed']!.messages.length
      api.messages.mockRejectedValueOnce(new Error('boom'))

      await store.refreshMessages('thread-closed')

      expect(store.conversations['thread-closed']!.messages).toHaveLength(before)
    })
  })
})
