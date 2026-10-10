import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { useChatStore } from '~/stores/chat'
import { buildMockBuyerHome } from '~/mocks/buyerHome'
import { emptyBuyerHome, type LiveCounts } from '~/utils/buyerHome'
import type { ChatThreadSummary } from '~/utils/chat'
import type { PostListItem, PostStatusInfo } from '~/composables/api/usePostsApi'
import HomePage from '~/pages/home.vue'
import LiveStatus from '~/components/request/LiveStatus.vue'
import RecentActivity from '~/components/buyer/RecentActivity.vue'
import RecentChats from '~/components/buyer/RecentChats.vue'

const { load, list, status, chatThreads, chatUnread } = vi.hoisted(() => ({
  load: vi.fn(),
  list: vi.fn(),
  status: vi.fn(),
  chatThreads: vi.fn(),
  chatUnread: vi.fn(),
}))

mockNuxtImport('useBuyerHomeApi', () => () => ({ load }))
mockNuxtImport('usePostsApi', () => () => ({ list, status }))
mockNuxtImport('useChatApi', () => () => ({ threads: chatThreads, unreadCount: chatUnread }))

function thread(id: string, overrides: Partial<ChatThreadSummary> = {}): ChatThreadSummary {
  return {
    id,
    post: { id: 'req-1', title: 'Szukam mikrofonu Shure SM7B', status: 'Active' },
    counterpart: { id: `m-${id}`, displayName: `Sklep ${id}` },
    lastMessage: { preview: `Wiadomość ${id}`, createdAt: new Date().toISOString(), isMine: false },
    unreadCount: 0,
    isLocked: false,
    createdAt: new Date().toISOString(),
    ...overrides,
  }
}

const minutesAgo = (n: number) => new Date(Date.now() - n * 60_000).toISOString()

function post(overrides: Partial<PostListItem>): PostListItem {
  return {
    id: 'req-1',
    title: 'Szukam mikrofonu Shure SM7B',
    description: 'Nowy lub w stanie bardzo dobrym.',
    latitude: 50.06,
    longitude: 19.94,
    radiusKm: 20,
    category: { id: 'c1', name: 'Audio i muzyka' },
    tag: { id: 't1', name: 'Mikrofon' },
    status: 'Active',
    notificationDispatchStatus: 'Dispatched',
    isUrgent: false,
    urgentDeadline: null,
    expiresAt: new Date(Date.now() + 72 * 3_600_000).toISOString(),
    isLongLived: false,
    createdAt: minutesAgo(12),
    notifiedCount: 14,
    ...overrides,
  }
}

function statusFor(notifiedCount: number, overrides: Partial<PostStatusInfo> = {}): PostStatusInfo {
  return {
    notificationDispatchStatus: 'Dispatched',
    notifiedCount,
    checkingCount: 0,
    haveItCount: 0,
    mayHaveItCount: 0,
    canOrderItCount: 0,
    cannotHelpCount: 0,
    isZeroMatch: notifiedCount === 0,
    ...overrides,
  }
}

const microphone = post({})
const iphone = post({ id: 'req-3', title: 'Szukam używanego iPhone 14 Pro', radiusKm: null, notifiedCount: 22, createdAt: minutesAgo(190) })

const mounted: Array<{ unmount: () => void }> = []

async function mountHome() {
  const wrapper = await mountSuspended(HomePage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

describe('Buyer home page', () => {
  beforeEach(() => {
    useAuthStore().setAuth('t', { id: '1', email: 'buyer-test@gdziekupic.local', role: 'Buyer' })
    load.mockReset().mockResolvedValue(buildMockBuyerHome())
    list.mockReset().mockResolvedValue([microphone, iphone])
    status.mockReset().mockImplementation(async (id: string) => statusFor(id === 'req-1' ? 14 : 22))
    chatThreads.mockReset().mockResolvedValue({ items: [], nextCursor: null })
    chatUnread.mockReset().mockResolvedValue(0)
    useChatStore().reset()
    clearNuxtData('buyer-home')
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('greets the buyer and selects the most recent real request by default', async () => {
    const wrapper = await mountHome()

    expect(list).toHaveBeenCalledWith('active')
    expect(wrapper.text()).toContain('Welcome back, Buyer!')
    expect(wrapper.find('h2.text-lg').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('14')
    expect(status).toHaveBeenCalledWith('req-1')
  })

  it('takes the live counts from the status endpoint', async () => {
    status.mockResolvedValue(statusFor(16, { checkingCount: 2, cannotHelpCount: 1 }))
    const wrapper = await mountHome()

    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('16')
  })

  it('updates summary, live status and activity when another request is selected', async () => {
    const wrapper = await mountHome()

    const iphoneCard = wrapper.findAll('button[aria-pressed]').find(b => b.text().includes('iPhone 14 Pro'))
    await iphoneCard!.trigger('click')
    await flushPromises()

    expect(wrapper.find('h2.text-lg').text()).toBe('Szukam używanego iPhone 14 Pro')
    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('22')
    expect(status).toHaveBeenCalledWith('req-3')
    expect(wrapper.text()).toContain('iStore Wrocław')
    expect(wrapper.text()).not.toContain('Audio Pro')
  })

  it('shows a call to action and hides the widgets when there are no requests', async () => {
    list.mockResolvedValue([])
    load.mockResolvedValue(emptyBuyerHome())
    const wrapper = await mountHome()

    expect(wrapper.text()).toContain('You have no active requests')
    expect(wrapper.find('[data-testid="notified-count"]').exists()).toBe(false)
    expect(wrapper.findAll('a[href="/requests/new"]').length).toBeGreaterThan(0)
  })

  it('offers a retry when loading fails', async () => {
    list.mockRejectedValue(new Error('boom'))
    const wrapper = await mountHome()

    expect(wrapper.text()).toContain('Could not load your dashboard.')
    expect(wrapper.text()).toContain('Try again')
  })
})

describe('Buyer home widgets', () => {
  const live: LiveCounts = { isLive: true, notifiedCount: 14, checkingCount: 3, haveCount: 2, cannotCount: 4 }
  const closed: LiveCounts = { isLive: false, notifiedCount: 8, checkingCount: 0, haveCount: 1, cannotCount: 7 }

  it('Live status lists the four buckets with the notified total', async () => {
    const wrapper = await mountSuspended(LiveStatus, { props: { request: live } })
    const text = wrapper.text()

    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('14')
    for (const label of ['Checking availability', 'Have it', 'Can\'t help', 'No response yet']) {
      expect(text).toContain(label)
    }
    expect(text).toContain('Live')
  })

  it('Live status marks a request that stopped collecting responses', async () => {
    const wrapper = await mountSuspended(LiveStatus, { props: { request: closed } })

    expect(wrapper.text()).toContain('Complete')
    expect(wrapper.text().replace('Live status', '')).not.toContain('Live')
  })

  it('Recent activity renders an empty state', async () => {
    const activity = await mountSuspended(RecentActivity, { props: { events: [] } })

    expect(activity.text()).toContain('No merchant activity yet.')
  })

  it('Live status splits the positive answers when the status has them', async () => {
    const split: LiveCounts = { ...live, haveCount: 6, haveItCount: 3, mayHaveItCount: 2, canOrderItCount: 1 }
    const wrapper = await mountSuspended(LiveStatus, { props: { request: split } })

    const rows = wrapper.findAll('[data-testid="status-row"]')
    expect(rows.map(row => row.attributes('data-key'))).toEqual(['checking', 'have', 'may_have', 'can_order', 'cannot', 'none'])
    expect(rows.map(row => row.find('span:last-child').text())).toEqual(['3', '3', '2', '1', '4', '1'])
    expect(wrapper.text()).toContain('May have it')
    expect(wrapper.text()).toContain('Can order it')
  })

  it('Recent activity describes each response state', async () => {
    const events = buildMockBuyerHome().activity.filter(e => e.requestId === 'req-1')
    const wrapper = await mountSuspended(RecentActivity, { props: { events } })

    expect(wrapper.text()).toContain('Has offered')
    expect(wrapper.text()).toContain('Is checking availability')
    expect(wrapper.text()).toContain('Can\'t help')
  })
})

describe('Buyer home recent chats', () => {
  const chats: Array<{ unmount: () => void }> = []

  async function mountChats() {
    const wrapper = await mountSuspended(RecentChats)
    await flushPromises()
    chats.push(wrapper)
    return wrapper
  }

  beforeEach(() => {
    useAuthStore().setAuth('t', { id: '1', email: 'buyer-test@gdziekupic.local', role: 'Buyer' })
    chatThreads.mockReset().mockResolvedValue({ items: [], nextCursor: null })
    chatUnread.mockReset().mockResolvedValue(0)
    useChatStore().reset()
  })

  afterEach(() => {
    chats.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('explains where conversations come from when there are none', async () => {
    const wrapper = await mountChats()

    expect(wrapper.find('[data-testid="chats-empty"]').text()).toContain('No conversations yet.')
    expect(wrapper.find('[data-testid="chats-view-all"]').exists()).toBe(false)
  })

  it('lists the latest four real threads as links with their unread count', async () => {
    chatThreads.mockResolvedValue({
      items: [
        thread('t1', { unreadCount: 3 }),
        thread('t2'),
        thread('t3'),
        thread('t4'),
        thread('t5'),
      ],
      nextCursor: null,
    })
    const wrapper = await mountChats()

    const rows = wrapper.findAll('[data-testid="chat-thread-row"]')
    expect(rows).toHaveLength(4)
    expect(rows[0]!.attributes('href')).toBe('/chat/t1')
    expect(rows[0]!.text()).toContain('Sklep t1')
    expect(rows[0]!.find('[data-testid="chat-thread-unread"]').text()).toBe('3')
    expect(rows[1]!.find('[data-testid="chat-thread-unread"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="chats-view-all"]').attributes('href')).toBe('/chat')
  })

  it('offers a retry when the threads cannot be loaded', async () => {
    chatThreads.mockRejectedValueOnce(new Error('boom'))
    const wrapper = await mountChats()

    expect(wrapper.text()).toContain('Try again')

    chatThreads.mockResolvedValue({ items: [thread('t1')], nextCursor: null })
    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="chat-thread-row"]')).toHaveLength(1)
  })
})
