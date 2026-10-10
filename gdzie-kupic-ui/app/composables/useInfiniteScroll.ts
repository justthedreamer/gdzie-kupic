import type { Ref } from 'vue'

// Calls `onReached` whenever `target` scrolls into view — the sentinel below a
// list that loads its next page on demand. IntersectionObserver reports an
// element that stays in view only once, so it is re-armed whenever `refresh`
// changes (e.g. the list grew): a page that does not fill the screen keeps
// loading until it does or runs out. Without IntersectionObserver (old
// browsers, unit tests) nothing happens; the list must offer a button then.
export function useInfiniteScroll(
  target: Ref<HTMLElement | null>,
  onReached: () => void,
  options: { rootMargin?: string, refresh?: () => unknown } = {},
) {
  let observer: IntersectionObserver | null = null

  function stop() {
    observer?.disconnect()
    observer = null
  }

  function start(element: HTMLElement | null) {
    stop()
    if (!element || typeof IntersectionObserver === 'undefined') return

    observer = new IntersectionObserver((entries) => {
      if (entries.some(entry => entry.isIntersecting)) onReached()
    }, { rootMargin: options.rootMargin ?? '200px' })
    observer.observe(element)
  }

  watch(target, start, { flush: 'post' })
  watch(() => options.refresh?.(), () => start(target.value), { flush: 'post' })
  onBeforeUnmount(stop)
}
