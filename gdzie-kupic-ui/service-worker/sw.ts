/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core'
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'
import { buildNotification, clickPath, NOTIFICATION_CLICK_MESSAGE } from '../app/utils/push'

// The app's service worker (strategy `injectManifest`, see `pwa` in nuxt.config.ts): the
// precache the PWA always had, plus Web Push. What to show and where a tap leads is decided by
// the pure functions in app/utils/push.ts (covered by unit tests).

declare const self: ServiceWorkerGlobalScope

// Updates take over at once (`registerType: 'autoUpdate'`).
void self.skipWaiting()
clientsClaim()

precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

try {
  registerRoute(new NavigationRoute(createHandlerBoundToURL('/')))
}
catch {
  // The server-rendered start page is not precached: navigations go to the network.
}

async function showPush(event: PushEvent) {
  let data: unknown = null
  try {
    data = event.data?.json()
  }
  catch {
    // Not JSON: ignored below like any malformed payload.
  }

  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
  const notification = buildNotification(data, windows)

  if (notification) await self.registration.showNotification(notification.title, notification.options)
}

self.addEventListener('push', (event) => {
  event.waitUntil(showPush(event))
})

async function openPage(path: string) {
  const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
  const open = windows.find(client => new URL(client.url).origin === self.location.origin)

  if (open) {
    // The app's session lives in memory, so the window must not be reloaded: it navigates itself.
    await open.focus()
    open.postMessage({ type: NOTIFICATION_CLICK_MESSAGE, url: path })
    return
  }

  await self.clients.openWindow(path)
}

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  event.waitUntil(openPage(clickPath(event.notification.data)))
})
