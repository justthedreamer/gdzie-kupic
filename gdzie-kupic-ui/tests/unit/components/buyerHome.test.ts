import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { buildMockBuyerHome } from '~/mocks/buyerHome'
import { emptyBuyerHome, type BuyerHomeData } from '~/utils/buyerHome'
import HomePage from '~/pages/home.vue'
import LiveStatus from '~/components/request/LiveStatus.vue'
import RecentActivity from '~/components/buyer/RecentActivity.vue'
import RecentChats from '~/components/buyer/RecentChats.vue'

const load = vi.hoisted(() => vi.fn())

mockNuxtImport('useBuyerHomeApi', () => () => ({ load }))

const mounted: Array<{ unmount: () => void }> = []

async function mountHome() {
  const wrapper = await mountSuspended(HomePage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

async function openHome(data: BuyerHomeData) {
  load.mockResolvedValue(data)
  return mountHome()
}

describe('Buyer home page', () => {
  beforeEach(() => {
    useAuthStore().setAuth('t', { id: '1', email: 'buyer-test@gdziekupic.local', role: 'Buyer' })
    load.mockReset()
    clearNuxtData('buyer-home')
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('greets the buyer and selects the most recent request by default', async () => {
    const wrapper = await openHome(buildMockBuyerHome())

    expect(wrapper.text()).toContain('Welcome back, Buyer!')
    // Summary card heading + the strip card both show the newest request.
    expect(wrapper.find('h2.text-lg').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('14')
  })

  it('updates summary, live status and activity when another request is selected', async () => {
    const wrapper = await openHome(buildMockBuyerHome())

    const iphoneCard = wrapper.findAll('button[aria-pressed]').find(b => b.text().includes('iPhone 14 Pro'))
    await iphoneCard!.trigger('click')

    expect(wrapper.find('h2.text-lg').text()).toBe('Szukam używanego iPhone 14 Pro')
    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('22')
    expect(wrapper.text()).toContain('iStore Wrocław')
    expect(wrapper.text()).not.toContain('Audio Pro')
  })

  it('shows a call to action and hides the widgets when there are no requests', async () => {
    const wrapper = await openHome(emptyBuyerHome())

    expect(wrapper.text()).toContain('You have no active requests')
    expect(wrapper.find('[data-testid="notified-count"]').exists()).toBe(false)
    expect(wrapper.findAll('a[href="/requests/new"]').length).toBeGreaterThan(0)
  })

  it('offers a retry when loading fails', async () => {
    load.mockRejectedValue(new Error('boom'))
    const wrapper = await mountHome()

    expect(wrapper.text()).toContain('Could not load your dashboard.')
    expect(wrapper.text()).toContain('Try again')
  })
})

describe('Buyer home widgets', () => {
  const sample = buildMockBuyerHome().requests
  const live = sample[0]!
  const closed = sample[1]!

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
