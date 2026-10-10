import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import type { PostStatusInfo } from '~/composables/api/usePostsApi'
import { usePostStatus } from '~/composables/usePostStatus'
import { useRealtimeStore } from '~/stores/realtime'

const { statusCall } = vi.hoisted(() => ({ statusCall: vi.fn() }))

mockNuxtImport('usePostsApi', () => () => ({ status: statusCall }))

function status(overrides: Partial<PostStatusInfo> = {}): PostStatusInfo {
  return {
    notificationDispatchStatus: 'Dispatched',
    notifiedCount: 5,
    checkingCount: 0,
    haveItCount: 0,
    mayHaveItCount: 0,
    canOrderItCount: 0,
    cannotHelpCount: 0,
    isZeroMatch: false,
    ...overrides,
  }
}

describe('usePostStatus with real-time events', () => {
  const id = ref('p1')
  const enabled = ref(true)
  let api: ReturnType<typeof usePostStatus>
  let realtime: ReturnType<typeof useRealtimeStore>
  const mounted: Array<{ unmount: () => void }> = []

  async function mountComposable() {
    const wrapper = await mountSuspended(defineComponent({
      setup() {
        realtime = useRealtimeStore()
        api = usePostStatus(id, enabled)
        return () => h('div')
      },
    }))
    mounted.push(wrapper)
    await flushPromises()
  }

  beforeEach(() => {
    id.value = 'p1'
    enabled.value = true
    statusCall.mockReset().mockResolvedValue(status())
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
    vi.useRealTimers()
  })

  it('reloads the status when its own request changed, and ignores other requests', async () => {
    await mountComposable()
    expect(statusCall).toHaveBeenCalledTimes(1)

    statusCall.mockResolvedValue(status({ haveItCount: 2 }))
    realtime.emit('postStatusChanged', { postId: 'other' })
    await flushPromises()
    expect(statusCall).toHaveBeenCalledTimes(1)

    realtime.emit('postStatusChanged', { postId: 'p1' })
    await flushPromises()
    expect(statusCall).toHaveBeenCalledTimes(2)
    expect(api.status.value?.haveItCount).toBe(2)
  })

  it('takes the counts from the server, not from the event', async () => {
    await mountComposable()
    statusCall.mockResolvedValue(status({ notificationDispatchStatus: 'Pending', notifiedCount: 0 }))

    realtime.emit('postStatusChanged', { postId: 'p1' })
    await flushPromises()

    expect(api.status.value?.notificationDispatchStatus).toBe('Pending')
  })

  it('still reloads on an event after the request ended (polling is off)', async () => {
    await mountComposable()
    enabled.value = false
    await flushPromises()

    realtime.emit('postStatusChanged', { postId: 'p1' })
    await flushPromises()

    expect(statusCall).toHaveBeenCalledTimes(2)
  })

  it('reloads after a resync', async () => {
    await mountComposable()

    realtime.emit('resync')
    await flushPromises()

    expect(statusCall).toHaveBeenCalledTimes(2)
  })

  it('stops listening when unmounted', async () => {
    await mountComposable()
    mounted.pop()!.unmount()

    realtime.emit('postStatusChanged', { postId: 'p1' })
    realtime.emit('resync')
    await flushPromises()

    expect(statusCall).toHaveBeenCalledTimes(1)
  })

  it('polls only while the connection is not up', async () => {
    vi.useFakeTimers()
    await mountComposable() // first load; the next poll comes after 30 s (dispatched)
    realtime.setState('connected')

    await vi.advanceTimersByTimeAsync(31_000)
    expect(statusCall).toHaveBeenCalledTimes(1)

    realtime.setState('reconnecting')
    await vi.advanceTimersByTimeAsync(31_000)
    expect(statusCall).toHaveBeenCalledTimes(2)
  })
})
