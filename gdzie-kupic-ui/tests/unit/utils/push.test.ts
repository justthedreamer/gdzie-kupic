import { describe, it, expect } from 'vitest'
import {
  buildNotification,
  clickPath,
  isNotConfigured,
  notificationPath,
  NOTIFICATION_CLICK_MESSAGE,
  parseClickMessage,
  parsePushPayload,
  toSubscriptionBody,
  urlBase64ToUint8Array,
} from '~/utils/push'

const message = (overrides: Record<string, unknown> = {}) => ({
  kind: 'merchantResponded',
  postId: 'p1',
  threadId: null,
  title: 'Sklep ma produkt',
  body: 'Rower górski',
  ...overrides,
})

describe('parsePushPayload', () => {
  it('accepts a payload of the contract', () => {
    expect(parsePushPayload(message())).toEqual(message())
  })

  it('treats a missing or blank id as null and a missing body as empty', () => {
    expect(parsePushPayload(message({ postId: ' ', threadId: undefined, body: undefined })))
      .toEqual(message({ postId: null, threadId: null, body: '' }))
  })

  it.each([
    ['null', null],
    ['a string', 'newPost'],
    ['an unknown kind', message({ kind: 'bogus' })],
    ['no kind', message({ kind: undefined })],
    ['no title', message({ title: undefined })],
    ['a blank title', message({ title: '  ' })],
  ])('rejects %s', (_name, data) => {
    expect(parsePushPayload(data)).toBeNull()
  })
})

describe('notificationPath', () => {
  it('leads a new post to the merchant feed entry', () => {
    expect(notificationPath({ kind: 'newPost', postId: 'p1', threadId: null })).toBe('/feed/p1')
  })

  it('leads a merchant response to the buyer\'s request', () => {
    expect(notificationPath({ kind: 'merchantResponded', postId: 'p1', threadId: 't1' })).toBe('/requests/p1')
  })

  it('leads a new message to the conversation', () => {
    expect(notificationPath({ kind: 'newMessage', postId: null, threadId: 't1' })).toBe('/chat/t1')
  })

  it('has no page when the id it needs is missing', () => {
    expect(notificationPath({ kind: 'newPost', postId: null, threadId: 't1' })).toBeNull()
    expect(notificationPath({ kind: 'newMessage', postId: 'p1', threadId: null })).toBeNull()
  })

  it('encodes the id', () => {
    expect(notificationPath({ kind: 'newPost', postId: 'a/b?c', threadId: null })).toBe('/feed/a%2Fb%3Fc')
  })
})

describe('buildNotification', () => {
  it('shows the title and text of the payload and remembers where a tap leads', () => {
    const shown = buildNotification(message(), [{ focused: false }])

    expect(shown?.title).toBe('Sklep ma produkt')
    expect(shown?.options.body).toBe('Rower górski')
    expect(shown?.options.data.url).toBe('/requests/p1')
  })

  it('shows it when the app has no window at all', () => {
    expect(buildNotification(message(), [])).not.toBeNull()
  })

  it('shows nothing while one of the app\'s windows is focused', () => {
    expect(buildNotification(message(), [{ focused: false }, { focused: true }])).toBeNull()
  })

  it('ignores a malformed payload', () => {
    expect(buildNotification(null, [])).toBeNull()
    expect(buildNotification({ kind: 'newPost' }, [])).toBeNull()
  })

  it('falls back to the start page when the payload has no id to lead to', () => {
    expect(buildNotification(message({ postId: null }), [])?.options.data.url).toBe('/')
  })
})

describe('clickPath / parseClickMessage', () => {
  it('keeps a path of the app', () => {
    expect(clickPath({ url: '/chat/t1' })).toBe('/chat/t1')
  })

  it.each([
    ['an absolute URL', { url: 'https://evil.example/' }],
    ['a protocol-relative URL', { url: '//evil.example/' }],
    ['no url', {}],
    ['no data', undefined],
  ])('falls back to the start page for %s', (_name, data) => {
    expect(clickPath(data)).toBe('/')
  })

  it('reads the notification-click message of the service worker and nothing else', () => {
    expect(parseClickMessage({ type: NOTIFICATION_CLICK_MESSAGE, url: '/chat/t1' })).toBe('/chat/t1')
    expect(parseClickMessage({ type: 'other', url: '/chat/t1' })).toBeNull()
    expect(parseClickMessage('hello')).toBeNull()
    expect(parseClickMessage(null)).toBeNull()
  })
})

describe('urlBase64ToUint8Array', () => {
  it('decodes a URL-safe base64 key without padding', () => {
    // "-_" are the URL-safe forms of "+/".
    expect(Array.from(urlBase64ToUint8Array('-_8'))).toEqual([251, 255])
    expect(Array.from(urlBase64ToUint8Array('AQID'))).toEqual([1, 2, 3])
  })
})

describe('toSubscriptionBody', () => {
  it('keeps the endpoint and both keys', () => {
    expect(toSubscriptionBody({ endpoint: 'https://push/1', keys: { p256dh: 'k', auth: 'a', extra: 'x' } }))
      .toEqual({ endpoint: 'https://push/1', keys: { p256dh: 'k', auth: 'a' } })
  })

  it('is null without an endpoint or keys', () => {
    expect(toSubscriptionBody({ keys: { p256dh: 'k', auth: 'a' } })).toBeNull()
    expect(toSubscriptionBody({ endpoint: 'https://push/1' })).toBeNull()
    expect(toSubscriptionBody({ endpoint: 'https://push/1', keys: { p256dh: 'k' } })).toBeNull()
  })
})

describe('isNotConfigured', () => {
  it('recognises the answers of a server without VAPID keys', () => {
    expect(isNotConfigured(503)).toBe(true)
    expect(isNotConfigured(404)).toBe(true)
    expect(isNotConfigured(500)).toBe(false)
    expect(isNotConfigured(null)).toBe(false)
  })
})
