import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { useMerchantFeedStore } from '~/stores/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'
import FeedPage from '~/pages/feed/index.vue'
import DetailsPage from '~/pages/feed/[id].vue'
import ResponseButtons from '~/components/merchant/ResponseButtons.vue'

const api = vi.hoisted(() => ({ load: vi.fn(), respond: vi.fn() }))

mockNuxtImport('useMerchantFeedApi', () => () => api)

const mounted: Array<{ unmount: () => void }> = []

async function openFeed() {
  const wrapper = await mountSuspended(FeedPage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

async function openDetails(id: string) {
  const wrapper = await mountSuspended(DetailsPage, { route: `/feed/${id}` })
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const cards = (wrapper: Awaited<ReturnType<typeof openFeed>>) => wrapper.findAll('[data-testid="feed-card"]')

describe('Merchant feed page', () => {
  beforeEach(() => {
    useAuthStore().setAuth('t', { id: 'm1', email: 'merchant-test@gdziekupic.local', role: 'Merchant' })
    useMerchantFeedStore().reset()
    api.load.mockReset().mockImplementation(async () => buildMockMerchantFeed())
    api.respond.mockReset().mockResolvedValue(undefined)
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('opens on the New tab with only unanswered requests, urgent first', async () => {
    const wrapper = await openFeed()
    const titles = cards(wrapper).map(card => card.find('h2').text())

    expect(titles[0]).toBe('Pilnie: kable XLR 5 m, 4 sztuki')
    expect(titles).not.toContain('Wzmacniacz gitarowy lampowy do 50 W')
    expect(cards(wrapper).length).toBe(useMerchantFeedStore().requests.filter(r => r.myResponse === null).length)
  })

  it('shows the number of unanswered requests on the New tab', async () => {
    const wrapper = await openFeed()
    const unanswered = useMerchantFeedStore().requests.filter(r => r.myResponse === null).length

    expect(wrapper.find('[role="tab"]').text()).toContain(String(unanswered))
  })

  it('lists answered requests on the Responded tab with their response', async () => {
    const wrapper = await openFeed()

    await wrapper.findAll('[role="tab"]')[1]!.trigger('mousedown', { button: 0 })
    await flushPromises()

    const text = wrapper.text()
    expect(cards(wrapper).length).toBe(3)
    expect(text).toContain('Wzmacniacz gitarowy lampowy do 50 W')
    expect(text).not.toContain('Pilnie: kable XLR')
  })

  it('moves a card from New to Responded after a response button is clicked', async () => {
    const wrapper = await openFeed()
    const before = cards(wrapper).length
    const first = cards(wrapper)[0]!
    const title = first.find('h2').text()

    await first.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(api.respond).toHaveBeenCalledTimes(1)
    expect(cards(wrapper).length).toBe(before - 1)
    expect(wrapper.text()).not.toContain(title)

    await wrapper.findAll('[role="tab"]')[1]!.trigger('mousedown', { button: 0 })
    await flushPromises()
    expect(wrapper.text()).toContain(title)
  })

  it('shows an empty state when there are no requests at all', async () => {
    api.load.mockResolvedValue([])
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('No requests in your area yet')
    expect(cards(wrapper)).toHaveLength(0)
    expect(wrapper.find('[role="tab"]').exists()).toBe(false)
  })

  it('explains an empty tab', async () => {
    api.load.mockImplementation(async () => buildMockMerchantFeed().map(r => ({ ...r, myResponse: 'HaveIt' as const })))
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('No new requests.')
  })

  it('offers a retry when loading fails', async () => {
    api.load.mockRejectedValue(new Error('boom'))
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('Could not load requests.')
    expect(wrapper.text()).toContain('Try again')
  })
})

describe('Merchant request details page', () => {
  beforeEach(() => {
    useAuthStore().setAuth('t', { id: 'm1', email: 'merchant-test@gdziekupic.local', role: 'Merchant' })
    useMerchantFeedStore().reset()
    api.load.mockReset().mockImplementation(async () => buildMockMerchantFeed())
    api.respond.mockReset().mockResolvedValue(undefined)
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('shows the request, its Live Status and an empty response', async () => {
    const wrapper = await openDetails('feed-1')

    expect(wrapper.find('h1').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.find('[data-testid="notified-count"]').text()).toBe('20')
    expect(wrapper.find('[data-testid="current-response"]').text()).toContain('haven\'t responded')
    expect(wrapper.text()).toContain('Verified')
  })

  it('records a response and shows it', async () => {
    const wrapper = await openDetails('feed-1')

    await wrapper.find('button[aria-label="I may have it"]').trigger('click')
    await flushPromises()

    expect(api.respond).toHaveBeenCalledWith('feed-1', 'MayHaveIt')
    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I may have it')
    expect(wrapper.find('button[aria-label="I may have it"]').attributes('aria-pressed')).toBe('true')
  })

  it('shows the existing response of an answered request', async () => {
    const wrapper = await openDetails('feed-5')

    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I have it')
  })

  it('shows a not-found state for an unknown id', async () => {
    const wrapper = await openDetails('does-not-exist')

    expect(wrapper.text()).toContain('Request not found.')
    expect(wrapper.find('h1').exists()).toBe(false)
  })
})

describe('Merchant response buttons', () => {
  it('emits the chosen state and highlights the current one', async () => {
    const wrapper = await mountSuspended(ResponseButtons, { props: { current: 'CantHelp' } })

    expect(wrapper.find('button[aria-label="I can\'t help"]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.find('button[aria-label="I have it"]').attributes('aria-pressed')).toBe('false')

    await wrapper.find('button[aria-label="I have it"]').trigger('click')

    expect(wrapper.emitted('select')).toEqual([['HaveIt']])
  })
})
