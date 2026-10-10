import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { buildMockBuyerHome } from '~/mocks/buyerHome'
import { emptyBuyerHome, type LiveCounts } from '~/utils/buyerHome'
import type { PostListItem, PostStatusInfo } from '~/composables/api/usePostsApi'
import HomePage from '~/pages/home.vue'
import LiveStatus from '~/components/request/LiveStatus.vue'
import RecentActivity from '~/components/buyer/RecentActivity.vue'
import RecentChats from '~/components/buyer/RecentChats.vue'

const { load, list, status } = vi.hoisted(() => ({ load: vi.fn(), list: vi.fn(), status: vi.fn() }))

mockNuxtImport('useBuyerHomeApi', () => () => ({ load }))
mockNuxtImport('usePostsApi', () => () => ({ list, status }))

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

  it('Recent activity and chats render empty states', async () => {
    const activity = await mountSuspended(RecentActivity, { props: { events: [] } })
    const chats = await mountSuspended(RecentChats, { props: { chats: [] } })

    expect(activity.text()).toContain('No merchant activity yet.')
    expect(chats.text()).toContain('No conversations yet.')
  })

  it('Recent activity describes each response state', async () => {
    const events = buildMockBuyerHome().activity.filter(e => e.requestId === 'req-1')
    const wrapper = await mountSuspended(RecentActivity, { props: { events } })

    expect(wrapper.text()).toContain('Has offered')
    expect(wrapper.text()).toContain('Is checking availability')
    expect(wrapper.text()).toContain('Can\'t help')
  })
})
