import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore, type UserRole } from '~/stores/auth'
import { useChatStore } from '~/stores/chat'
import { MockChat } from '~/mocks/chat'
import { MAX_MESSAGE_LENGTH } from '~/utils/chat'
import Composer from '~/components/chat/Composer.vue'
import MessageBubble from '~/components/chat/MessageBubble.vue'
import InboxPage from '~/pages/chat/index.vue'
import ThreadPage from '~/pages/chat/[id].vue'

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

function signIn(role: UserRole) {
  server = new MockChat(role, 'u1')
  api.threads.mockReset().mockImplementation(async (cursor: string | null, limit?: number) => server.listThreads(cursor, limit))
  api.thread.mockReset().mockImplementation(async (id: string) => server.getThread(id))
  api.messages.mockReset().mockImplementation(async (id: string, query?: object) => server.getMessages(id, query))
  api.send.mockReset().mockImplementation(async (id: string, body: string, image?: File | null) => server.sendMessage(id, body, image))
  api.markRead.mockReset().mockImplementation(async (id: string) => server.markRead(id))
  api.unreadCount.mockReset().mockImplementation(async () => server.unreadCount())
  useAuthStore().setAuth('t', { id: 'u1', email: 'u@test', role })
  useChatStore().reset()
}

const mounted: Array<{ unmount: () => void }> = []

async function mountPage<T extends object>(page: T, route: string) {
  const wrapper = await mountSuspended(page, { route })
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

afterEach(() => {
  while (mounted.length) mounted.pop()!.unmount()
})

describe('ChatComposer', () => {
  async function mountComposer(text: string, locked = false) {
    const wrapper = await mountSuspended(Composer, {
      props: {
        locked,
        modelValue: text,
        'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
      },
    })
    mounted.push(wrapper)
    return wrapper
  }

  it('disables sending for empty text and enables it for text', async () => {
    const wrapper = await mountComposer('')
    expect(wrapper.find('[data-testid="chat-send"]').attributes('disabled')).toBeDefined()

    await wrapper.setProps({ modelValue: 'Cześć' })
    expect(wrapper.find('[data-testid="chat-send"]').attributes('disabled')).toBeUndefined()
  })

  it('sends with Enter, but not with Shift+Enter', async () => {
    const wrapper = await mountComposer('Cześć')
    const textarea = wrapper.find('textarea')

    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })
    expect(wrapper.emitted('send')).toBeUndefined()

    await textarea.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('send')).toHaveLength(1)
  })

  it('does not send empty text with Enter', async () => {
    const wrapper = await mountComposer('   ')

    await wrapper.find('textarea').trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('send')).toBeUndefined()
  })

  it('shows the counter near the limit and blocks sending over it', async () => {
    const wrapper = await mountComposer('a'.repeat(MAX_MESSAGE_LENGTH + 1))

    expect(wrapper.find('[data-testid="chat-counter"]').text()).toContain(`${MAX_MESSAGE_LENGTH + 1}/${MAX_MESSAGE_LENGTH}`)
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="chat-send"]').attributes('disabled')).toBeDefined()

    await wrapper.find('textarea').trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('send')).toBeUndefined()
  })

  it('shows no counter for a short message', async () => {
    const wrapper = await mountComposer('Cześć')

    expect(wrapper.find('[data-testid="chat-counter"]').exists()).toBe(false)
  })

  it('is replaced by the explanation in a locked thread', async () => {
    const wrapper = await mountComposer('', true)

    expect(wrapper.find('[data-testid="chat-locked"]').exists()).toBe(true)
    expect(wrapper.find('textarea').exists()).toBe(false)
  })
})

describe('ChatComposer image', () => {
  const png = () => new File(['x'], 'a.png', { type: 'image/png' })

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
  })

  async function mountComposer(text = '', refused: string | null = null) {
    const wrapper = await mountSuspended(Composer, {
      props: {
        locked: false,
        modelValue: text,
        'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
        image: null as File | null,
        'onUpdate:image': (value: File | null) => wrapper.setProps({ image: value }),
        refused,
        'onUpdate:refused': (value: string | null) => wrapper.setProps({ refused: value }),
      },
    })
    mounted.push(wrapper)
    return wrapper
  }

  async function choose(wrapper: Awaited<ReturnType<typeof mountComposer>>, file: File) {
    const input = wrapper.get('[data-testid="chat-attach-input"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
    await input.trigger('change')
  }

  it('accepts only the image types of the server', async () => {
    const wrapper = await mountComposer()

    expect(wrapper.get('[data-testid="chat-attach-input"]').attributes('accept')).toBe('image/jpeg,image/png,image/webp')
  })

  it('shows a preview of the chosen image, and removing it clears it', async () => {
    const wrapper = await mountComposer()
    const file = png()

    await choose(wrapper, file)

    expect(wrapper.emitted('update:image')?.[0]).toEqual([file])
    expect(wrapper.get('[data-testid="chat-attachment-preview"] img').attributes('src')).toBe('blob:preview')

    await wrapper.get('[data-testid="chat-attachment-remove"]').trigger('click')

    expect(wrapper.find('[data-testid="chat-attachment-preview"]').exists()).toBe(false)
    expect(wrapper.emitted('update:image')?.at(-1)).toEqual([null])
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview')
  })

  it('can send an image without text', async () => {
    const wrapper = await mountComposer()
    expect(wrapper.get('[data-testid="chat-send"]').attributes('disabled')).toBeDefined()

    await choose(wrapper, png())
    expect(wrapper.get('[data-testid="chat-send"]').attributes('disabled')).toBeUndefined()

    await wrapper.get('form').trigger('submit')
    expect(wrapper.emitted('send')).toHaveLength(1)
  })

  it('refuses a file of another type with a message and does not attach it', async () => {
    const wrapper = await mountComposer()

    await choose(wrapper, new File(['x'], 'a.gif', { type: 'image/gif' }))

    expect(wrapper.get('[data-testid="chat-attachment-error"]').text()).toContain('JPEG, PNG or WebP')
    expect(wrapper.emitted('update:image')).toBeUndefined()
    expect(wrapper.find('[data-testid="chat-attachment-preview"]').exists()).toBe(false)
  })

  it('refuses a file that is too large, naming the limit', async () => {
    const wrapper = await mountComposer()
    const big = png()
    Object.defineProperty(big, 'size', { value: 6 * 1024 * 1024 })

    await choose(wrapper, big)

    expect(wrapper.get('[data-testid="chat-attachment-error"]').text()).toContain('5 MB')
    expect(wrapper.emitted('update:image')).toBeUndefined()
  })

  it('clears the message when a valid file is chosen afterwards', async () => {
    const wrapper = await mountComposer()
    await choose(wrapper, new File(['x'], 'a.gif', { type: 'image/gif' }))

    await choose(wrapper, png())

    expect(wrapper.find('[data-testid="chat-attachment-error"]').exists()).toBe(false)
  })

  it('shows what the server refused until the image is removed', async () => {
    const wrapper = await mountComposer('', 'too_large')
    await choose(wrapper, png())
    // Choosing another image resets the verdict about the previous one.
    expect(wrapper.find('[data-testid="chat-attachment-error"]').exists()).toBe(false)

    await wrapper.setProps({ refused: 'unsupported_type' })
    expect(wrapper.get('[data-testid="chat-attachment-error"]').text()).toContain('JPEG, PNG or WebP')

    await wrapper.get('[data-testid="chat-attachment-remove"]').trigger('click')
    expect(wrapper.find('[data-testid="chat-attachment-error"]').exists()).toBe(false)
  })
})

describe('ChatMessageBubble', () => {
  const base = { body: 'Dzień dobry', createdAt: '2026-01-01T12:00:00Z', author: 'Audio Shop' }

  it('tells own and incoming messages apart', async () => {
    const own = await mountSuspended(MessageBubble, { props: { ...base, own: true } })
    const incoming = await mountSuspended(MessageBubble, { props: { ...base, own: false } })

    expect(own.get('[data-testid="chat-message"]').attributes('data-own')).toBe('true')
    expect(incoming.get('[data-testid="chat-message"]').attributes('data-own')).toBe('false')
    expect(incoming.text()).toContain('Audio Shop')
    expect(incoming.text()).toContain('Dzień dobry')
  })

  it('offers retry and discard only for a failed message', async () => {
    const failed = await mountSuspended(MessageBubble, { props: { ...base, own: true, state: 'failed' } })
    const sending = await mountSuspended(MessageBubble, { props: { ...base, own: true, state: 'sending' } })

    await failed.get('[data-testid="chat-retry"]').trigger('click')
    await failed.get('[data-testid="chat-discard"]').trigger('click')

    expect(failed.emitted('retry')).toHaveLength(1)
    expect(failed.emitted('discard')).toHaveLength(1)
    expect(sending.find('[data-testid="chat-retry"]').exists()).toBe(false)
  })

  it('shows no picture for a text message', async () => {
    const wrapper = await mountSuspended(MessageBubble, { props: { ...base, own: false } })

    expect(wrapper.find('[data-testid="chat-image"]').exists()).toBe(false)
  })

  it('shows the picture of the message, with and without text', async () => {
    const incoming = await mountSuspended(MessageBubble, { props: { ...base, own: false, attachmentUrl: 'https://files.test/a.png' } })
    const own = await mountSuspended(MessageBubble, { props: { ...base, body: null, own: true, attachmentUrl: 'https://files.test/b.png' } })

    expect(incoming.get('[data-testid="chat-image"]').attributes()).toMatchObject({ src: 'https://files.test/a.png', alt: 'Photo from Audio Shop' })
    expect(incoming.text()).toContain('Dzień dobry')
    expect(own.get('[data-testid="chat-image"]').attributes('alt')).toBe('Your photo')
    expect(own.find('p.whitespace-pre-wrap').exists()).toBe(false)
  })

  it('opens the picture enlarged on click', async () => {
    const wrapper = await mountSuspended(MessageBubble, { props: { ...base, own: false, attachmentUrl: 'https://files.test/a.png' } })
    mounted.push(wrapper)
    expect(document.body.querySelector('[data-testid="chat-image-large"]')).toBeNull()

    await wrapper.get('[data-testid="chat-image-open"]').trigger('click')
    await flushPromises()

    const large = document.body.querySelector('[data-testid="chat-image-large"]')
    expect(large?.getAttribute('src')).toBe('https://files.test/a.png')
    expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()
  })

  it('shows a fallback when the picture fails to load, and asks for a fresh URL on reload', async () => {
    const wrapper = await mountSuspended(MessageBubble, { props: { ...base, own: false, attachmentUrl: 'https://files.test/old.png' } })

    await wrapper.get('[data-testid="chat-image"]').trigger('error')

    expect(wrapper.find('[data-testid="chat-image"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="chat-image-fallback"]').text()).toContain('could not be loaded')

    await wrapper.get('[data-testid="chat-image-reload"]').trigger('click')

    expect(wrapper.emitted('reloadImage')).toHaveLength(1)
    expect(wrapper.find('[data-testid="chat-image"]').exists()).toBe(true)
  })

  it('tries the picture again when the server hands out a new URL', async () => {
    const wrapper = await mountSuspended(MessageBubble, { props: { ...base, own: false, attachmentUrl: 'https://files.test/old.png' } })
    await wrapper.get('[data-testid="chat-image"]').trigger('error')

    await wrapper.setProps({ attachmentUrl: 'https://files.test/new.png' })

    expect(wrapper.get('[data-testid="chat-image"]').attributes('src')).toBe('https://files.test/new.png')
  })
})

describe('inbox page', () => {
  beforeEach(() => signIn('Buyer'))

  it('lists the threads with the counterpart, the request, the preview and the unread count', async () => {
    const wrapper = await mountPage(InboxPage, '/chat')
    const rows = wrapper.findAll('[data-testid="chat-thread-row"]')

    expect(rows).toHaveLength(3)
    const first = rows.find(row => row.attributes('href') === '/chat/thread-feed-4')!
    expect(first.text()).toContain('Audio Shop Kraków')
    expect(first.text()).toContain('Słuchawki studyjne Beyerdynamic DT 770 Pro')
    expect(first.get('[data-testid="chat-thread-unread"]').text()).toBe('2')
    expect(first.text()).not.toContain('You:')

    const second = rows.find(row => row.attributes('href') === '/chat/thread-closed')!
    expect(second.find('[data-testid="chat-thread-unread"]').exists()).toBe(false)
    expect(second.text()).toContain('You:')
  })

  it('marks locked threads', async () => {
    const wrapper = await mountPage(InboxPage, '/chat')
    const locked = wrapper.findAll('[data-testid="chat-thread-row"]').find(row => row.attributes('href') === '/chat/thread-locked')!

    expect(locked.text()).toContain('Locked')
  })

  it('explains the empty inbox per role', async () => {
    api.threads.mockResolvedValue({ items: [], nextCursor: null })
    const buyer = await mountPage(InboxPage, '/chat')
    expect(buyer.get('[data-testid="chat-empty"]').text()).toContain('merchant answers')

    signIn('Merchant')
    api.threads.mockResolvedValue({ items: [], nextCursor: null })
    const merchant = await mountPage(InboxPage, '/chat')
    expect(merchant.get('[data-testid="chat-empty"]').text()).toContain('you answer a request')
  })

  it('shows an error with a retry', async () => {
    api.threads.mockRejectedValueOnce(new Error('boom'))
    const wrapper = await mountPage(InboxPage, '/chat')

    expect(wrapper.text()).toContain('Could not load the conversations.')

    await wrapper.find('button').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('[data-testid="chat-thread-row"]')).toHaveLength(3)
  })

  it('offers "show more" when there is another page and loads it', async () => {
    api.threads.mockImplementation(async (cursor: string | null) => server.listThreads(cursor, 2))
    const wrapper = await mountPage(InboxPage, '/chat')
    expect(wrapper.findAll('[data-testid="chat-thread-row"]')).toHaveLength(2)

    await wrapper.get('[data-testid="chat-more"] button').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="chat-thread-row"]')).toHaveLength(3)
    expect(wrapper.find('[data-testid="chat-more"]').exists()).toBe(false)
  })
})

describe('thread page', () => {
  beforeEach(() => signIn('Buyer'))

  it('shows the header and the latest messages, own and incoming apart', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')

    expect(wrapper.get('[data-testid="chat-counterpart"]').text()).toBe('Studio Sklep')
    expect(wrapper.get('[data-testid="chat-request-link"]').attributes('href')).toBe('/requests/closed')
    const bubbles = wrapper.findAll('[data-testid="chat-message"]')
    expect(bubbles.map(bubble => bubble.attributes('data-own'))).toEqual(['true', 'false', 'true'])
    expect(wrapper.get('[data-testid="chat-request-inactive"]').text()).toContain('Closed')
  })

  it('links a merchant to the request in the feed', async () => {
    signIn('Merchant')
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')

    expect(wrapper.get('[data-testid="chat-counterpart"]').text()).toBe('Anna')
    expect(wrapper.get('[data-testid="chat-request-link"]').attributes('href')).toBe('/feed/closed')
    expect(wrapper.findAll('[data-testid="chat-message"]').map(bubble => bubble.attributes('data-own'))).toEqual(['false', 'true', 'false'])
  })

  it('offers older messages when the history is longer than a page, and loads them', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/thread-feed-4')
    expect(wrapper.findAll('[data-testid="chat-message"]')).toHaveLength(30)

    await wrapper.get('[data-testid="chat-older"] button').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="chat-message"]')).toHaveLength(47)
    expect(wrapper.find('[data-testid="chat-older"]').exists()).toBe(false)
  })

  it('marks the thread read when opened', async () => {
    await mountPage(ThreadPage, '/chat/thread-feed-4')

    expect(api.markRead).toHaveBeenCalledWith('thread-feed-4')
    expect(useChatStore().conversations['thread-feed-4']!.thread?.unreadCount).toBe(0)
  })

  it('sends a message, clears the box and shows the message', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')

    await wrapper.get('textarea').setValue('Dziękuję za odpowiedź')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(api.send).toHaveBeenCalledWith('thread-closed', 'Dziękuję za odpowiedź', null)
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('')
    const bubbles = wrapper.findAll('[data-testid="chat-message"]')
    expect(bubbles).toHaveLength(4)
    expect(bubbles.at(-1)!.text()).toContain('Dziękuję za odpowiedź')
    expect(bubbles.at(-1)!.attributes('data-state')).toBe('sent')
  })

  it('keeps a message that failed to send, with retry', async () => {
    api.send.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusCode: 500 }))
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')

    await wrapper.get('textarea').setValue('Halo?')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const failed = wrapper.findAll('[data-testid="chat-message"]').at(-1)!
    expect(failed.attributes('data-state')).toBe('failed')

    await failed.get('[data-testid="chat-retry"]').trigger('click')
    await flushPromises()

    const last = wrapper.findAll('[data-testid="chat-message"]').at(-1)!
    expect(last.attributes('data-state')).toBe('sent')
    expect(last.text()).toContain('Halo?')
  })

  async function chooseOnPage(wrapper: Awaited<ReturnType<typeof mountPage>>, file: File) {
    const input = wrapper.get('[data-testid="chat-attach-input"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
    await input.trigger('change')
  }

  it('sends a picture with text as multipart, clears the composer and shows the picture', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')
    const file = new File(['x'], 'a.png', { type: 'image/png' })

    await chooseOnPage(wrapper, file)
    await wrapper.get('textarea').setValue('Takie?')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(api.send).toHaveBeenCalledWith('thread-closed', 'Takie?', file)
    expect(wrapper.find('[data-testid="chat-attachment-preview"]').exists()).toBe(false)
    const last = wrapper.findAll('[data-testid="chat-message"]').at(-1)!
    expect(last.text()).toContain('Takie?')
    expect(last.get('[data-testid="chat-image"]').attributes('src')).toBe('blob:preview')
  })

  it('gives text and picture back with the reason when the server refuses the picture', async () => {
    URL.createObjectURL = vi.fn(() => 'blob:preview')
    URL.revokeObjectURL = vi.fn()
    api.send.mockRejectedValueOnce(Object.assign(new Error('too large'), { statusCode: 413 }))
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')
    const file = new File(['x'], 'a.png', { type: 'image/png' })

    await chooseOnPage(wrapper, file)
    await wrapper.get('textarea').setValue('Takie?')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="chat-message"]')).toHaveLength(3)
    expect((wrapper.get('textarea').element as HTMLTextAreaElement).value).toBe('Takie?')
    expect(wrapper.find('[data-testid="chat-attachment-preview"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="chat-attachment-error"]').text()).toContain('too large')
  })

  it('asks the server for fresh pictures when one no longer loads', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')
    sessionStorage.setItem('gk:mock-chat-incoming', JSON.stringify([{ threadId: 'thread-closed', body: null, attachmentUrl: 'https://files.test/old.png' }]))
    await useChatStore().poll('thread-closed')
    await flushPromises()

    const image = wrapper.get('[data-testid="chat-image"]')
    expect(image.attributes('src')).toBe('https://files.test/old.png')
    await image.trigger('error')
    api.messages.mockClear()
    await wrapper.get('[data-testid="chat-image-reload"]').trigger('click')
    await flushPromises()

    // The latest page again, the way the thread is opened.
    expect(api.messages).toHaveBeenCalledWith('thread-closed')
  })

  it('replaces the composer with an explanation in a locked thread', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/thread-locked')

    expect(wrapper.get('[data-testid="chat-locked"]').text()).toContain('locked')
    expect(wrapper.find('textarea').exists()).toBe(false)
  })

  it('says so when the thread does not exist', async () => {
    const wrapper = await mountPage(ThreadPage, '/chat/nope')

    expect(wrapper.get('[data-testid="chat-not-found"]').text()).toContain('Conversation not found')
  })

  it('shows an error with a retry when the thread cannot be loaded', async () => {
    api.thread.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusCode: 500 }))
    const wrapper = await mountPage(ThreadPage, '/chat/thread-closed')
    expect(wrapper.text()).toContain('Could not load the conversation.')

    await wrapper.findAll('button').find(button => button.text().includes('Try again'))!.trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="chat-counterpart"]').text()).toBe('Studio Sklep')
  })
})
