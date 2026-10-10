import { PUSH_BANNER_DISMISSED_KEY, pushBannerMode } from '~/utils/notificationSettings'

function readDismissed(): boolean {
  try {
    return localStorage.getItem(PUSH_BANNER_DISMISSED_KEY) === '1'
  }
  catch {
    return false
  }
}

/**
 * What the push banner should show on this device (see `pushBannerMode`) and a way to dismiss it
 * for good. Reads the state of push on mount; stays hidden until the dismissal flag is read.
 */
export function usePushBanner() {
  const push = usePushStore()
  const dismissed = ref(true)

  onMounted(() => {
    dismissed.value = readDismissed()
    if (push.state === 'unknown') void push.refresh()
  })

  const mode = computed(() => pushBannerMode(push.state, push.permission, dismissed.value))

  function dismiss() {
    dismissed.value = true
    try {
      localStorage.setItem(PUSH_BANNER_DISMISSED_KEY, '1')
    }
    catch {
      // Private mode: the banner just comes back next time.
    }
  }

  return { mode, dismiss }
}
