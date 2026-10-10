import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref } from 'vue'
import type { PostResponse } from '~/composables/api/usePostsApi'
import { usePostResponses } from '~/composables/usePostResponses'
import Responses from '~/components/request/Responses.vue'

const { responsesCall } = vi.hoisted(() => ({ responsesCall: vi.fn() }))

mockNuxtImport('usePostsApi', () => () => ({ responses: responsesCall }))

function response(merchantId: string, overrides: Partial<PostResponse> = {}): PostResponse {
  return {
    merchantId,
    shopName: `Sklep ${merchantId}`,
    state: 'HaveIt',
    threadId: `thread-${merchantId}`,
    unreadCount: 0,
    updatedAt: '2026-01-01T10:00:00Z',
    ...overrides,
  }
}

const mounted: Array<{ unmount: () => void }> = []

afterEach(() => {
  mounted.splice(0).forEach(wrapper => wrapper.unmount())
})

describe('usePostResponses', () => {
  // `tick` stands in for the status: undefined until its first load, then a new value per refresh.
  const tick = ref<unknown>(undefined)
  const id = ref('p1')
  let api: ReturnType<typeof usePostResponses>

  async function mountComposable() {
    const wrapper = await mountSuspended(defineComponent({
      setup() {
        api = usePostResponses(id, () => tick.value)
        return () => h('div')
      },
    }))
    mounted.push(wrapper)
    return wrapper
  }

  beforeEach(() => {
    tick.value = undefined
    id.value = 'p1'
    responsesCall.mockReset().mockResolvedValue([])
  })

  it('waits for the first status load before asking', async () => {
    await mountComposable()
    await flushPromises()

    expect(responsesCall).not.toHaveBeenCalled()
    expect(api.loaded.value).toBe(false)
  })

  it('loads once the status loaded and again with every status refresh, newest answer first', async () => {
    responsesCall.mockResolvedValue([
      response('old', { updatedAt: '2026-01-01T08:00:00Z' }),
      response('new', { updatedAt: '2026-01-01T12:00:00Z' }),
    ])
    await mountComposable()

    tick.value = { n: 1 }
    await flushPromises()

    expect(responsesCall).toHaveBeenCalledWith('p1')
    expect(api.loaded.value).toBe(true)
    expect(api.responses.value.map(r => r.merchantId)).toEqual(['new', 'old'])

    tick.value = { n: 2 }
    await flushPromises()
    expect(responsesCall).toHaveBeenCalledTimes(2)
  })

  it('also loads when the status itself failed (the list does not depend on it)', async () => {
    await mountComposable()

    tick.value = null
    await flushPromises()

    expect(responsesCall).toHaveBeenCalledTimes(1)
    expect(api.loaded.value).toBe(true)
  })

  it('keeps the last list when a refresh fails and recovers afterwards', async () => {
    responsesCall.mockResolvedValueOnce([response('a')])
    await mountComposable()
    tick.value = { n: 1 }
    await flushPromises()

    responsesCall.mockRejectedValueOnce(new Error('boom'))
    tick.value = { n: 2 }
    await flushPromises()

    expect(api.error.value).not.toBeNull()
    expect(api.responses.value.map(r => r.merchantId)).toEqual(['a'])

    responsesCall.mockResolvedValueOnce([response('a'), response('b')])
    await api.refresh()
    expect(api.error.value).toBeNull()
    expect(api.responses.value).toHaveLength(2)
  })

  it('forgets the list when another request is opened', async () => {
    responsesCall.mockResolvedValue([response('a')])
    await mountComposable()
    tick.value = { n: 1 }
    await flushPromises()

    id.value = 'p2'
    await nextTick()

    expect(api.responses.value).toEqual([])
    expect(api.loaded.value).toBe(false)
  })

  it('does not apply an answer that arrives after unmount or after a newer request', async () => {
    let resolveFirst: (value: PostResponse[]) => void = () => {}
    responsesCall
      .mockReturnValueOnce(new Promise<PostResponse[]>((resolve) => { resolveFirst = resolve }))
      .mockResolvedValueOnce([response('fresh')])
    await mountComposable()

    tick.value = { n: 1 }
    await nextTick()
    tick.value = { n: 2 }
    await flushPromises()
    resolveFirst([response('stale')])
    await flushPromises()

    expect(api.responses.value.map(r => r.merchantId)).toEqual(['fresh'])
  })
})

describe('RequestResponses', () => {
  async function mountList(props: { responses: PostResponse[], loaded?: boolean, failed?: boolean }) {
    const wrapper = await mountSuspended(Responses, { props: { loaded: true, failed: false, ...props } })
    mounted.push(wrapper)
    return wrapper
  }

  it('shows a loading line until the first load finished', async () => {
    const wrapper = await mountList({ responses: [], loaded: false })

    expect(wrapper.find('[data-testid="responses-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="responses-empty"]').exists()).toBe(false)
  })

  it('explains that nobody has confirmed yet', async () => {
    const wrapper = await mountList({ responses: [] })

    expect(wrapper.find('[data-testid="responses-empty"]').text()).toContain('Nobody has confirmed')
  })

  it('offers a retry when the first load failed', async () => {
    const wrapper = await mountList({ responses: [], failed: true })

    expect(wrapper.text()).toContain('Could not load the merchant responses.')
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('lists each merchant with the answer and a link into its thread', async () => {
    const wrapper = await mountList({
      responses: [
        response('a', { state: 'HaveIt', unreadCount: 2 }),
        response('b', { state: 'MayHaveIt' }),
        response('c', { state: 'CanOrderIt' }),
      ],
    })

    const rows = wrapper.findAll('[data-testid="response-row"]')
    expect(rows).toHaveLength(3)
    expect(rows[0]!.find('a').attributes('href')).toBe('/chat/thread-a')
    expect(rows[0]!.text()).toContain('Sklep a')
    expect(rows[0]!.find('[data-testid="response-state"]').text()).toBe('Has it')
    expect(rows[0]!.find('[data-testid="response-unread"]').text()).toBe('2')
    expect(rows[1]!.find('[data-testid="response-state"]').text()).toBe('May have it')
    expect(rows[1]!.find('[data-testid="response-unread"]').exists()).toBe(false)
    expect(rows[2]!.find('[data-testid="response-state"]').text()).toBe('Can order it')
  })

  it('keeps the list and says it may be outdated when a refresh failed', async () => {
    const wrapper = await mountList({ responses: [response('a')], failed: true })

    expect(wrapper.findAll('[data-testid="response-row"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('Could not refresh the status.')
  })
})
