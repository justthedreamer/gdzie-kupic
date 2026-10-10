import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { useRealtimeStore } from '~/stores/realtime'
import { useRealtimeEvent } from '~/composables/useRealtimeEvent'

describe('realtime store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts disconnected and reports connected only in the connected state', () => {
    const store = useRealtimeStore()
    expect(store.state).toBe('disconnected')
    expect(store.connected).toBe(false)

    store.setState('reconnecting')
    expect(store.connected).toBe(false)

    store.setState('connected')
    expect(store.connected).toBe(true)
  })

  it('delivers an event with its payload to every subscriber of that name only', () => {
    const store = useRealtimeStore()
    const a = vi.fn()
    const b = vi.fn()
    const other = vi.fn()
    store.on('postAdded', a)
    store.on('postAdded', b)
    store.on('postRemoved', other)

    store.emit('postAdded', { postId: 'p1' })

    expect(a).toHaveBeenCalledWith({ postId: 'p1' })
    expect(b).toHaveBeenCalledWith({ postId: 'p1' })
    expect(other).not.toHaveBeenCalled()
  })

  it('stops delivering after unsubscribing', () => {
    const store = useRealtimeStore()
    const handler = vi.fn()
    const off = store.on('threadUpdated', handler)

    store.emit('threadUpdated', { threadId: 't1' })
    off()
    store.emit('threadUpdated', { threadId: 't2' })

    expect(handler).toHaveBeenCalledTimes(1)
  })

  it('allows a handler to unsubscribe while the event is delivered', () => {
    const store = useRealtimeStore()
    const second = vi.fn()
    const off = store.on('resync', () => off())
    store.on('resync', second)

    store.emit('resync')

    expect(second).toHaveBeenCalledTimes(1)
  })

  it('keeps delivering to the other subscribers when one handler throws', () => {
    const store = useRealtimeStore()
    const error = vi.spyOn(console, 'error').mockImplementation(() => {})
    const healthy = vi.fn()
    store.on('resync', () => { throw new Error('boom') })
    store.on('resync', healthy)

    store.emit('resync')

    expect(healthy).toHaveBeenCalledTimes(1)
    expect(error).toHaveBeenCalled()
    error.mockRestore()
  })
})

describe('useRealtimeEvent', () => {
  it('subscribes while the component is mounted and unsubscribes when it unmounts', async () => {
    const handler = vi.fn()
    let store!: ReturnType<typeof useRealtimeStore>
    const wrapper = await mountSuspended(defineComponent({
      setup() {
        // The store of the app the component runs in.
        store = useRealtimeStore()
        useRealtimeEvent('messageReceived', handler)
        return () => h('div')
      },
    }))

    store.emit('messageReceived', { threadId: 't1', messageId: 'm1' })
    expect(handler).toHaveBeenCalledWith({ threadId: 't1', messageId: 'm1' })

    wrapper.unmount()
    store.emit('messageReceived', { threadId: 't1', messageId: 'm2' })
    expect(handler).toHaveBeenCalledTimes(1)
  })
})
