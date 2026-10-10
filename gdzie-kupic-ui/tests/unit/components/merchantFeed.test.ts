import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { useMerchantFeedStore } from '~/stores/merchantFeed'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'
import { unansweredCount, type MerchantFeedRequest, type MerchantResponse } from '~/utils/merchantFeed'
import FeedPage from '~/pages/feed/index.vue'
import DetailsPage from '~/pages/feed/[id].vue'
import ResponseButtons from '~/components/merchant/ResponseButtons.vue'
import { serveFeed } from '../support/merchantFeedServer'

const api = vi.hoisted(() => ({
  list: vi.fn(),
  summary: vi.fn(),
  get: vi.fn(),
  respond: vi.fn(),
  categories: vi.fn(),
}))

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
const tabs = (wrapper: Awaited<ReturnType<typeof openFeed>>) => wrapper.findAll('[role="tab"]')

async function selectTab(wrapper: Awaited<ReturnType<typeof openFeed>>, index: number) {
  await tabs(wrapper)[index]!.trigger('mousedown', { button: 0 })
  await flushPromises()
}

/** A fake backend over the mock data; a response changes what it serves afterwards. */
let feed: MerchantFeedRequest[]
/** Chat threads the fake backend has opened, by request id. */
let threads: Record<string, string>

function arrange(pageSize = 4) {
  feed = buildMockMerchantFeed()
  threads = {}
  api.list.mockReset().mockImplementation(serveFeed(() => feed, pageSize))
  api.summary.mockReset().mockImplementation(async () => {
    const newCount = unansweredCount(feed)
    return { newCount, respondedCount: feed.length - newCount }
  })
  api.get.mockReset().mockImplementation(async (id: string) => {
    const found = feed.find(item => item.id === id)
    if (!found) throw Object.assign(new Error('Not found'), { statusCode: 404 })
    return { ...found, threadId: threads[id] ?? null }
  })
  api.respond.mockReset().mockImplementation(async (id: string, state: MerchantResponse) => {
    if (feed.find(item => item.id === id)?.status !== 'Active') {
      throw Object.assign(new Error('post_not_active'), { statusCode: 409 })
    }
    feed = feed.map(item => (item.id === id ? { ...item, myResponse: state } : item))
    if (state !== 'CantHelp') threads[id] = `thread-${id}`
    return { state, threadId: threads[id] ?? null, updatedAt: '2026-01-01T00:00:00Z' }
  })
  api.categories.mockReset().mockResolvedValue([])
}

function signIn() {
  useAuthStore().setAuth('t', { id: 'm1', email: 'merchant-test@gdziekupic.local', role: 'Merchant' })
  useMerchantFeedStore().reset()
}

describe('Merchant feed page', () => {
  beforeEach(() => {
    signIn()
    arrange()
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('opens on the New tab with only unanswered requests, urgent first', async () => {
    const wrapper = await openFeed()
    const titles = cards(wrapper).map(card => card.find('h2').text())

    expect(api.list).toHaveBeenCalledWith(expect.objectContaining({ tab: 'new', sort: 'newest' }), null)
    expect(titles[0]).toBe('Pilnie: kable XLR 5 m, 4 sztuki')
    expect(titles).not.toContain('Wzmacniacz gitarowy lampowy do 50 W')
    expect(cards(wrapper)).toHaveLength(unansweredCount(feed))
  })

  it('shows the counts from the summary on the tabs', async () => {
    const wrapper = await openFeed()
    const newCount = unansweredCount(feed)

    expect(tabs(wrapper)[0]!.text()).toContain(String(newCount))
    expect(tabs(wrapper)[1]!.text()).toContain(String(feed.length - newCount))
    expect(tabs(wrapper)[2]!.text()).toContain(String(feed.length))
  })

  it('shows the distance, the buyer\'s first name and the urgency on a card, but no budget or city', async () => {
    const wrapper = await openFeed()
    const urgent = cards(wrapper)[0]!

    expect(urgent.text()).toContain('Urgent')
    expect(urgent.text()).toContain('7.8 km from you')
    expect(urgent.text()).toContain('Piotr')
    expect(urgent.text()).not.toMatch(/Kraków|PLN|zł|Verified/)
  })

  it('lists answered requests on the Responded tab with their response', async () => {
    const wrapper = await openFeed()

    await selectTab(wrapper, 1)

    expect(api.list).toHaveBeenLastCalledWith(expect.objectContaining({ tab: 'responded' }), null)
    expect(cards(wrapper)).toHaveLength(3)
    expect(wrapper.text()).toContain('Wzmacniacz gitarowy lampowy do 50 W')
    expect(wrapper.text()).not.toContain('Pilnie: kable XLR')
  })

  it('moves a card from New to Responded after a response button is clicked', async () => {
    const wrapper = await openFeed()
    const before = cards(wrapper).length
    const first = cards(wrapper)[0]!
    const title = first.find('h2').text()

    await first.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(api.respond).toHaveBeenCalledTimes(1)
    expect(cards(wrapper)).toHaveLength(before - 1)
    expect(wrapper.text()).not.toContain(title)
    expect(tabs(wrapper)[0]!.text()).toContain(String(before - 1))

    await selectTab(wrapper, 1)
    expect(wrapper.text()).toContain(title)
  })

  it('loads the next page with the "Show more" button until the feed ends', async () => {
    const wrapper = await openFeed()
    await selectTab(wrapper, 2)
    expect(cards(wrapper)).toHaveLength(4)

    await wrapper.find('[data-testid="feed-more"] button').trigger('click')
    await flushPromises()

    expect(cards(wrapper)).toHaveLength(feed.length)
    expect(wrapper.find('[data-testid="feed-more"]').exists()).toBe(false)
    expect(api.list).toHaveBeenLastCalledWith(expect.objectContaining({ tab: 'all' }), '4')
  })

  it('offers a retry when loading the next page fails and keeps the list', async () => {
    const wrapper = await openFeed()
    await selectTab(wrapper, 2)
    const serve = serveFeed(() => feed, 4)
    api.list.mockRejectedValueOnce(new Error('boom'))

    await wrapper.find('[data-testid="feed-more"] button').trigger('click')
    await flushPromises()

    expect(cards(wrapper)).toHaveLength(4)
    expect(wrapper.find('[data-testid="feed-more"]').text()).toContain('Could not load more requests.')

    api.list.mockImplementation(serve)
    await wrapper.find('[data-testid="feed-more"] button').trigger('click')
    await flushPromises()

    expect(cards(wrapper)).toHaveLength(feed.length)
  })

  it('shows an empty state when there are no requests at all', async () => {
    api.list.mockResolvedValue({ items: [], nextCursor: null })
    api.summary.mockResolvedValue({ newCount: 0, respondedCount: 0 })
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('No requests in your area yet')
    expect(cards(wrapper)).toHaveLength(0)
    expect(wrapper.find('[role="tab"]').exists()).toBe(false)
  })

  it('explains an empty tab', async () => {
    api.list.mockResolvedValue({ items: [], nextCursor: null })
    api.summary.mockResolvedValue({ newCount: 0, respondedCount: 3 })
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('No new requests.')
  })

  it('offers a retry when loading fails', async () => {
    api.list.mockRejectedValue(new Error('boom'))
    const wrapper = await openFeed()

    expect(wrapper.text()).toContain('Could not load requests.')
    expect(wrapper.text()).toContain('Try again')
  })

  it('says so and refreshes the list when the post can no longer be answered (409)', async () => {
    const wrapper = await openFeed()
    api.respond.mockRejectedValue(Object.assign(new Error('Conflict'), { statusCode: 409 }))
    const lists = api.list.mock.calls.length

    await cards(wrapper)[0]!.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('no longer open for responses')
    expect(api.list.mock.calls.length).toBeGreaterThan(lists)
  })

  it('shows an error and keeps the card when saving the response fails', async () => {
    const wrapper = await openFeed()
    const before = cards(wrapper).length
    api.respond.mockRejectedValue(new Error('boom'))

    await cards(wrapper)[0]!.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Could not save your response')
    expect(cards(wrapper)).toHaveLength(before)
  })
})

describe('Merchant request details page', () => {
  beforeEach(() => {
    signIn()
    arrange()
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('shows the request, the buyer\'s first name and an empty response', async () => {
    const wrapper = await openDetails('feed-1')

    expect(wrapper.find('h1').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.text()).toContain('Marek')
    expect(wrapper.text()).toContain('3.2 km from you')
    expect(wrapper.text()).toContain('radius 15 km')
    expect(wrapper.find('[data-testid="current-response"]').text()).toContain('haven\'t responded')
    expect(wrapper.text()).not.toMatch(/Verified|PLN|Kraków/)
  })

  it('fetches a request that is not on a loaded page (a direct link)', async () => {
    await openDetails('feed-7')

    expect(api.get).toHaveBeenCalledWith('feed-7')
  })

  it('shows the loaded request at once and refreshes it from the server', async () => {
    await useMerchantFeedStore().load()
    feed = feed.map(item => (item.id === 'feed-1' ? { ...item, status: 'Closed' as const } : item))
    const wrapper = await openDetails('feed-1')

    expect(api.get).toHaveBeenCalledWith('feed-1')
    expect(wrapper.find('h1').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.find('[data-testid="closed-notice"]').exists()).toBe(true)
  })

  it('explains an unlimited buyer radius', async () => {
    feed = feed.map(item => (item.id === 'feed-1' ? { ...item, buyerRadiusKm: null } : item))
    const wrapper = await openDetails('feed-1')

    expect(wrapper.text()).toContain('no radius limit')
  })

  it('records a response and shows it', async () => {
    const wrapper = await openDetails('feed-1')

    await wrapper.find('button[aria-label="I may have it"]').trigger('click')
    await flushPromises()

    expect(api.respond).toHaveBeenCalledWith('feed-1', 'MayHaveIt')
    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I may have it')
    expect(wrapper.find('button[aria-label="I may have it"]').attributes('aria-pressed')).toBe('true')
  })

  it('keeps showing a request answered from the loaded New tab', async () => {
    await useMerchantFeedStore().load()
    const wrapper = await openDetails('feed-1')

    await wrapper.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('h1').text()).toBe('Szukam mikrofonu Shure SM7B')
    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I have it')
  })

  it('shows an error when the response cannot be saved', async () => {
    const wrapper = await openDetails('feed-1')
    api.respond.mockRejectedValue(Object.assign(new Error('Conflict'), { statusCode: 409 }))

    await wrapper.find('button[aria-label="I may have it"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('no longer open for responses')
    expect(wrapper.find('[data-testid="current-response"]').text()).toContain('haven\'t responded')
  })

  it('shows the existing response of an answered request', async () => {
    const wrapper = await openDetails('feed-5')

    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I have it')
  })

  it('offers every response while the post is active and no thread link before answering', async () => {
    const wrapper = await openDetails('feed-1')

    expect(wrapper.findAll('button[aria-pressed]').every(button => button.attributes('disabled') === undefined)).toBe(true)
    expect(wrapper.find('[data-testid="closed-notice"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="open-thread"]').exists()).toBe(false)
  })

  it('links to the chat thread after a positive response, and keeps it after "can\'t help"', async () => {
    const wrapper = await openDetails('feed-1')

    await wrapper.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="open-thread"]').attributes('href')).toBe('/chat/thread-feed-1')

    await wrapper.find('button[aria-label="I can\'t help"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I can\'t help')
    expect(wrapper.find('[data-testid="open-thread"]').attributes('href')).toBe('/chat/thread-feed-1')
  })

  it('shows the thread link of an already answered request', async () => {
    threads['feed-5'] = 'thread-feed-5'
    const wrapper = await openDetails('feed-5')

    expect(wrapper.find('[data-testid="open-thread"]').attributes('href')).toBe('/chat/thread-feed-5')
  })

  it('disables the buttons of a closed post, explains why and keeps the thread link', async () => {
    feed = feed.map(item => (item.id === 'feed-5' ? { ...item, status: 'Expired' as const } : item))
    threads['feed-5'] = 'thread-feed-5'
    const wrapper = await openDetails('feed-5')

    const buttons = wrapper.findAll('button[aria-pressed]')
    expect(buttons).toHaveLength(4)
    expect(buttons.every(button => button.attributes('disabled') !== undefined)).toBe(true)
    expect(wrapper.find('[data-testid="closed-notice"]').text()).toContain('no longer active (Expired)')
    expect(wrapper.find('[data-testid="current-response"]').text()).toBe('I have it')
    expect(wrapper.find('[data-testid="open-thread"]').exists()).toBe(true)
  })

  it('re-synchronises when the post closed during the response (409)', async () => {
    const wrapper = await openDetails('feed-1')
    feed = feed.map(item => (item.id === 'feed-1' ? { ...item, status: 'Closed' as const } : item))

    await wrapper.find('button[aria-label="I have it"]').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('no longer open for responses')
    expect(wrapper.find('[data-testid="closed-notice"]').exists()).toBe(true)
    expect(wrapper.findAll('button[aria-pressed]').every(button => button.attributes('disabled') !== undefined)).toBe(true)
    expect(wrapper.find('[data-testid="current-response"]').text()).toContain('haven\'t responded')
  })

  it('sends one request per action: the buttons are disabled while saving', async () => {
    const wrapper = await openDetails('feed-1')
    let finish!: () => void
    api.respond.mockReset().mockImplementation(() => new Promise((resolve) => {
      finish = () => resolve({ state: 'HaveIt', threadId: null, updatedAt: '2026-01-01T00:00:00Z' })
    }))

    await wrapper.find('button[aria-label="I have it"]').trigger('click')
    await wrapper.find('button[aria-label="I have it"]').trigger('click')
    await wrapper.find('button[aria-label="I may have it"]').trigger('click')

    expect(api.respond).toHaveBeenCalledTimes(1)
    expect(wrapper.findAll('button[aria-pressed]').every(button => button.attributes('disabled') !== undefined)).toBe(true)

    finish()
    await flushPromises()
    expect(wrapper.findAll('button[aria-pressed]').every(button => button.attributes('disabled') === undefined)).toBe(true)
  })
  it('shows a not-found state for an unknown id', async () => {
    const wrapper = await openDetails('does-not-exist')

    expect(wrapper.text()).toContain('Request not found.')
    expect(wrapper.find('h1').exists()).toBe(false)
  })
})

describe('Merchant response buttons', () => {
  it('offers all four FR-RESP-1 states, best to worst', async () => {
    const wrapper = await mountSuspended(ResponseButtons, { props: { current: null } })

    expect(wrapper.findAll('button').map(button => button.attributes('aria-label'))).toEqual([
      'I have it',
      'I may have it',
      'I can order it',
      'I can\'t help',
    ])
  })

  it('disables every button when asked to', async () => {
    const wrapper = await mountSuspended(ResponseButtons, { props: { current: null, disabled: true } })

    expect(wrapper.findAll('button').every(button => button.attributes('disabled') !== undefined)).toBe(true)
  })
  it('emits CanOrderIt and highlights it when current', async () => {
    const wrapper = await mountSuspended(ResponseButtons, { props: { current: 'CanOrderIt' } })

    expect(wrapper.find('button[aria-label="I can order it"]').attributes('aria-pressed')).toBe('true')

    await wrapper.find('button[aria-label="I may have it"]').trigger('click')
    expect(wrapper.emitted('select')).toEqual([['MayHaveIt']])
  })

  it('emits the chosen state and highlights the current one', async () => {
    const wrapper = await mountSuspended(ResponseButtons, { props: { current: 'CantHelp' } })

    expect(wrapper.find('button[aria-label="I can\'t help"]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.find('button[aria-label="I have it"]').attributes('aria-pressed')).toBe('false')

    await wrapper.find('button[aria-label="I have it"]').trigger('click')

    expect(wrapper.emitted('select')).toEqual([['HaveIt']])
  })
})
