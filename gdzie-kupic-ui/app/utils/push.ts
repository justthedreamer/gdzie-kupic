// Web Push: the pure logic shared by the app and by the service worker
// (service-worker/sw.ts imports this file, so it must not use Nuxt auto-imports or the DOM).
// Contract: planning/phase-7-push-notifications.md and docs/api.md § Web Push.

export type PushKind = 'newPost' | 'merchantResponded' | 'newMessage'

/** The body of a Web Push message, built by the server. */
export interface PushPayload {
  kind: PushKind
  postId: string | null
  threadId: string | null
  title: string
  body: string
}

/**
 * State of push on this device.
 * - `unsupported`: the browser cannot do push (e.g. iOS outside an installed app).
 * - `notConfigured`: the server has no VAPID keys.
 * - `blocked`: the user denied the permission in the browser.
 * - `off` / `on`: the device is not / is registered for push.
 */
export type PushState = 'unsupported' | 'notConfigured' | 'blocked' | 'off' | 'on'

/** What the service stores for a device (`PUT /api/push/subscription`). */
export interface PushSubscriptionBody {
  endpoint: string
  keys: { p256dh: string, auth: string }
}

export const PUSH_ICON = '/pwa-192x192.png'

/** Message from the service worker to an open window: go to `url`. */
export const NOTIFICATION_CLICK_MESSAGE = 'notification-click'

const KINDS: readonly PushKind[] = ['newPost', 'merchantResponded', 'newMessage']

const idOrNull = (value: unknown): string | null =>
  typeof value === 'string' && value.trim() !== '' ? value : null

/** A valid payload, or `null` for anything malformed (the service worker then shows nothing). */
export function parsePushPayload(data: unknown): PushPayload | null {
  if (!data || typeof data !== 'object') return null

  const raw = data as Record<string, unknown>
  if (!KINDS.includes(raw.kind as PushKind)) return null
  if (typeof raw.title !== 'string' || raw.title.trim() === '') return null

  return {
    kind: raw.kind as PushKind,
    postId: idOrNull(raw.postId),
    threadId: idOrNull(raw.threadId),
    title: raw.title,
    body: typeof raw.body === 'string' ? raw.body : '',
  }
}

/** The page a notification leads to, or `null` when the payload lacks the id it needs. */
export function notificationPath(payload: Pick<PushPayload, 'kind' | 'postId' | 'threadId'>): string | null {
  switch (payload.kind) {
    case 'newPost':
      return payload.postId ? `/feed/${encodeURIComponent(payload.postId)}` : null
    case 'merchantResponded':
      return payload.postId ? `/requests/${encodeURIComponent(payload.postId)}` : null
    case 'newMessage':
      return payload.threadId ? `/chat/${encodeURIComponent(payload.threadId)}` : null
  }
}

/** Where a notification click goes when it has no specific page. */
export const FALLBACK_PATH = '/'

export interface DisplayedNotification {
  title: string
  options: { body: string, icon: string, badge: string, data: { url: string } }
}

/**
 * What the service worker shows for a push message: nothing when one of the app's windows is
 * focused (the in-app event already covers that case), nothing for a malformed payload.
 */
export function buildNotification(data: unknown, windows: ReadonlyArray<{ focused: boolean }>): DisplayedNotification | null {
  if (windows.some(client => client.focused)) return null

  const payload = parsePushPayload(data)
  if (!payload) return null

  return {
    title: payload.title,
    options: {
      body: payload.body,
      icon: PUSH_ICON,
      badge: PUSH_ICON,
      data: { url: notificationPath(payload) ?? FALLBACK_PATH },
    },
  }
}

/** A same-app path from the click data of a notification; anything else becomes the fallback. */
export function clickPath(data: unknown): string {
  const url = data && typeof data === 'object' ? (data as { url?: unknown }).url : undefined

  // Paths only: never an absolute or protocol-relative URL.
  return typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') ? url : FALLBACK_PATH
}

/** The path of a `notification-click` message from the service worker, or `null` for any other message. */
export function parseClickMessage(data: unknown): string | null {
  if (!data || typeof data !== 'object') return null

  const message = data as { type?: unknown, url?: unknown }

  return message.type === NOTIFICATION_CLICK_MESSAGE ? clickPath(message) : null
}

/** The VAPID public key (URL-safe base64) as the byte array `pushManager.subscribe` needs. */
export function urlBase64ToUint8Array(base64: string): Uint8Array<ArrayBuffer> {
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const binary = atob(padded.replace(/-/g, '+').replace(/_/g, '/'))
  const bytes = new Uint8Array(new ArrayBuffer(binary.length))

  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)

  return bytes
}

/** The registration body for a browser subscription; `null` when it lacks an endpoint or keys. */
export function toSubscriptionBody(subscription: { endpoint?: string, keys?: Record<string, string> }): PushSubscriptionBody | null {
  const { endpoint, keys } = subscription
  if (!endpoint || !keys?.p256dh || !keys.auth) return null

  return { endpoint, keys: { p256dh: keys.p256dh, auth: keys.auth } }
}

/** HTTP statuses the server answers the public-key request with when Web Push is not configured. */
export const NOT_CONFIGURED_STATUSES: readonly number[] = [404, 501, 503]

export function isNotConfigured(status: number | null): boolean {
  return status !== null && NOT_CONFIGURED_STATUSES.includes(status)
}
