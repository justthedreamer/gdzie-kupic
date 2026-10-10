// Real-time (SignalR) contract shared with the service: event names and payloads, the hub
// URL and the reconnect backoff. See docs/api.md § Real-time events. Every event carries
// identifiers only - the screens refetch the current state through REST (the database is the
// source of truth, SignalR is delivery only).

export const REALTIME_HUB_PATH = '/hubs/app'

export type RealtimeState = 'connected' | 'reconnecting' | 'disconnected'

export interface RealtimePayloads {
  /** Merchant: a post that matches the merchant has just been recorded. */
  postAdded: { postId: string }
  /** Merchant: a notified post moved to Closed, Fulfilled or Expired. */
  postRemoved: { postId: string }
  /** Buyer: the status, the counts or the responses of an own post changed. */
  postStatusChanged: { postId: string }
  /** The other side of a thread wrote a message. */
  messageReceived: { threadId: string, messageId: string }
  /** A thread was created or its locked state changed. */
  threadUpdated: { threadId: string }
  /** An in-app notification: `merchantResponded` (buyer) or `newMessage`. */
  notificationRaised: { kind: 'merchantResponded' | 'newMessage', postId: string | null, threadId: string | null }
  /**
   * Not sent by the server: emitted locally after every successful (re)connect so that every
   * subscriber refetches its data and closes the gap in which events could have been missed.
   */
  resync: undefined
}

export type RealtimeEventName = keyof RealtimePayloads

/** The events the server pushes (everything except the local `resync`). */
export const SERVER_EVENTS = [
  'postAdded',
  'postRemoved',
  'postStatusChanged',
  'messageReceived',
  'threadUpdated',
  'notificationRaised',
] as const satisfies readonly RealtimeEventName[]

export type RealtimeHandler<E extends RealtimeEventName> = (payload: RealtimePayloads[E]) => void

export function realtimeHubUrl(apiBase: string): string {
  return `${apiBase.replace(/\/+$/, '')}${REALTIME_HUB_PATH}`
}

const BACKOFF_MS = [0, 1000, 2000, 5000, 10_000, 30_000]

/** Delay before retry number `retry` (0-based): 0, 1, 2, 5, 10 s, then 30 s for ever. */
export function reconnectDelay(retry: number): number {
  return BACKOFF_MS[Math.min(Math.max(retry, 0), BACKOFF_MS.length - 1)]!
}
