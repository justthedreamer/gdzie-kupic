import { parseClickMessage } from '~/utils/push'
import { createBrowserPush, createFakePush, type FakePush, type FakePushState } from '~/utils/pushBrowser'

// Test hooks of the `pushMock` mode (see docs/api.md § Web Push): e2e tests play the browser's
// push API through `window.__push` and can preset it with `window.__pushInit` (before the app starts).
declare global {
  interface Window {
    __push?: FakePush
    __pushInit?: Partial<FakePushState>
  }
}

// Provides the browser's push API (or its fake), keeps this device registered for the signed-in
// user and follows notification clicks. Client-only: push needs the browser.
export default defineNuxtPlugin((nuxtApp) => {
  const config = useRuntimeConfig().public

  const pushBrowser = config.pushMock ? createFakePush(window.__pushInit) : createBrowserPush()
  if (config.pushMock) window.__push = pushBrowser as FakePush
  // Provided right away (not through the plugin's return value): the watcher below uses it at once.
  nuxtApp.provide('pushBrowser', pushBrowser)

  const auth = useAuthStore()
  const push = usePushStore()

  // Re-register the device after every sign-in (and when another account is switched to).
  watch(
    () => [auth.token, auth.user?.role] as const,
    ([token, role]) => {
      if (token && role && role !== 'Admin') void push.syncAfterSignIn()
    },
    { immediate: true },
  )

  // Tapping a notification while the app is open: the service worker focuses the window and asks it to
  // go to the page (a reload would lose the in-memory session).
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.addEventListener('message', (event) => {
      const path = parseClickMessage(event.data)
      if (path) void navigateTo(path)
    })
  }
})
