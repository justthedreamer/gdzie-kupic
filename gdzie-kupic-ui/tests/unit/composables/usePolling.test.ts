import { describe, it, expect, afterEach, vi } from 'vitest'
import { mountSuspended } from '@nuxt/test-utils/runtime'
import { defineComponent, h } from 'vue'
import { usePolling } from '~/composables/usePolling'
import { useRealtimeStore } from '~/stores/realtime'

function mountPolling(task: () => unknown, intervalMs = 1000, options: { immediate?: boolean, fallbackOnly?: boolean } = {}) {
  return mountSuspended(defineComponent({
    setup() {
      usePolling(task, intervalMs, options)
      return () => h('div')
    },
  }))
}

function setVisibility(state: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state })
  document.dispatchEvent(new Event('visibilitychange'))
}

describe('usePolling', () => {
  afterEach(() => {
    vi.useRealTimers()
    setVisibility('visible')
  })

  it('runs the task every interval while mounted, and stops after unmount', async () => {
    vi.useFakeTimers()
    const task = vi.fn()
    const wrapper = await mountPolling(task)

    expect(task).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(1000)
    expect(task).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(2000)
    expect(task).toHaveBeenCalledTimes(3)

    wrapper.unmount()
    await vi.advanceTimersByTimeAsync(5000)
    expect(task).toHaveBeenCalledTimes(3)
  })

  it('runs at once on mount with `immediate`', async () => {
    vi.useFakeTimers()
    const task = vi.fn()
    const wrapper = await mountPolling(task, 1000, { immediate: true })

    await vi.advanceTimersByTimeAsync(0)
    expect(task).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })

  it('does not overlap runs: a slow task delays the next tick', async () => {
    vi.useFakeTimers()
    let release: () => void = () => {}
    const task = vi.fn(() => new Promise<void>((resolve) => { release = resolve }))
    const wrapper = await mountPolling(task)

    await vi.advanceTimersByTimeAsync(1000)
    expect(task).toHaveBeenCalledTimes(1)
    await vi.advanceTimersByTimeAsync(3000)
    expect(task).toHaveBeenCalledTimes(1)

    release()
    await vi.advanceTimersByTimeAsync(1000)
    expect(task).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('keeps polling after the task fails', async () => {
    vi.useFakeTimers()
    const task = vi.fn().mockRejectedValue(new Error('boom'))
    const wrapper = await mountPolling(task)

    await vi.advanceTimersByTimeAsync(3000)

    expect(task).toHaveBeenCalledTimes(3)
    wrapper.unmount()
  })

  it('skips ticks while the tab is hidden and runs at once when it is back', async () => {
    vi.useFakeTimers()
    const task = vi.fn()
    const wrapper = await mountPolling(task)

    setVisibility('hidden')
    await vi.advanceTimersByTimeAsync(5000)
    expect(task).not.toHaveBeenCalled()

    setVisibility('visible')
    await vi.advanceTimersByTimeAsync(0)
    expect(task).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })

  it('with `fallbackOnly` skips ticks while the real-time connection is up and resumes when it drops', async () => {
    vi.useFakeTimers()
    const task = vi.fn()
    let realtime!: ReturnType<typeof useRealtimeStore>
    const wrapper = await mountSuspended(defineComponent({
      setup() {
        realtime = useRealtimeStore()
        usePolling(task, 1000, { fallbackOnly: true })
        return () => h('div')
      },
    }))

    realtime.setState('disconnected')
    await vi.advanceTimersByTimeAsync(1000)
    expect(task).toHaveBeenCalledTimes(1)

    realtime.setState('connected')
    await vi.advanceTimersByTimeAsync(5000)
    expect(task).toHaveBeenCalledTimes(1)

    realtime.setState('reconnecting')
    await vi.advanceTimersByTimeAsync(1000)
    expect(task).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })
})
