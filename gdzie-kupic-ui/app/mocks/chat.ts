import type { UserRole } from '~/stores/auth'
import { buildMockMerchantFeed } from '~/mocks/merchantFeed'
import {
  MESSAGES_PAGE_SIZE,
  THREADS_PAGE_SIZE,
  attachmentProblem,
  type ChatMessage,
  type ChatMessagePage,
  type ChatThreadPage,
  type ChatThreadSummary,
} from '~/utils/chat'

// Sample chat behind `chatMock`: an in-memory "server" that behaves like the chat
// endpoints (ascending message pages with `before` / `after`, unread counts, locked
// threads), so the pages work the same on mock data and on the real API.
//
// Two hooks let a test or a manual session play the other side:
//   sessionStorage['gk:mock-chat-incoming']  = JSON [{ "threadId": "...", "body": "...", "attachmentUrl"?: "..." }]
//     -> the counterpart's messages (with a picture, if given), delivered on the next request;
//   sessionStorage['gk:mock-chat-fail-send'] = '1'
//     -> sending fails with a 500 until the key is removed.

const MINUTE = 60_000

interface ThreadSeed {
  id: string
  title: string
  status: ChatThreadSummary['post']['status']
  /** What the buyer sees / what the merchant sees. */
  shop: string
  buyer: string
  locked?: boolean
  /** Messages as [fromBuyer, text], oldest first. */
  messages: Array<[boolean, string]>
  /** Appended for the merchant only, so that the unread ones are the buyer's. */
  merchantTail?: Array<[boolean, string]>
  /** The newest this many messages (from the counterpart) are unread. */
  unread: number
}

/** A long conversation, so that the history needs several pages. */
function longConversation(): Array<[boolean, string]> {
  const lines: Array<[boolean, string]> = [
    [true, 'Dzień dobry, czy słuchawki DT 770 Pro (250 Ω) są dostępne od ręki?'],
    [false, 'Dzień dobry! Tak, mamy jedną parę na magazynie.'],
  ]
  for (let i = 1; i <= 21; i++) {
    lines.push([true, `Pytanie ${i}: czy w zestawie jest kabel i pokrowiec?`])
    lines.push([false, `Odpowiedź ${i}: tak, w zestawie jest wszystko, co potrzebne.`])
  }
  lines.push([true, 'Świetnie, mogę odebrać jutro po południu?'])
  lines.push([false, 'Jutro do 18:00 jesteśmy w sklepie.'])
  lines.push([false, 'Czekamy na Pana, odłożę słuchawki.'])
  return lines
}

const SEEDS: ThreadSeed[] = [
  {
    id: 'thread-feed-4',
    title: 'Słuchawki studyjne Beyerdynamic DT 770 Pro',
    status: 'Active',
    shop: 'Audio Shop Kraków',
    buyer: 'Kasia',
    messages: longConversation(),
    merchantTail: [
      [true, 'Czy słuchawki nadal są odłożone?'],
      [true, 'Będę przed 16:00.'],
    ],
    unread: 2,
  },
  {
    id: 'thread-closed',
    title: 'Interfejs audio USB do domowego studia',
    status: 'Closed',
    shop: 'Studio Sklep',
    buyer: 'Anna',
    messages: [
      [true, 'Czy interfejs ma dwa wejścia XLR?'],
      [false, 'Tak, dwa wejścia combo XLR/jack.'],
      [true, 'Dziękuję, zapytanie zamknąłem, ale chętnie jeszcze popytam.'],
    ],
    unread: 0,
  },
  {
    id: 'thread-locked',
    title: 'Pilnie: kable XLR 5 m, 4 sztuki',
    status: 'Active',
    shop: 'Muzyczny Raj',
    buyer: 'Piotr',
    locked: true,
    messages: [
      [true, 'Potrzebuję czterech kabli na jutro.'],
      [false, 'Mamy trzy, czwarty na zamówienie.'],
    ],
    unread: 0,
  },
]

function buildThread(seed: ThreadSeed, role: UserRole, myId: string, now: number): { summary: ChatThreadSummary, messages: ChatMessage[] } {
  const iAmBuyer = role === 'Buyer'
  const counterpartId = `counterpart-${seed.id}`
  const lines = iAmBuyer ? seed.messages : [...seed.messages, ...(seed.merchantTail ?? [])]
  const count = lines.length

  const messages = lines.map(([fromBuyer, body], index): ChatMessage => {
    const mine = fromBuyer === iAmBuyer
    return {
      id: `${seed.id}-m${String(index + 1).padStart(3, '0')}`,
      threadId: seed.id,
      senderId: mine ? myId : counterpartId,
      isMine: mine,
      body,
      attachmentUrl: null,
      createdAt: new Date(now - (count - index) * 7 * MINUTE).toISOString(),
    }
  })

  return {
    summary: {
      id: seed.id,
      post: { id: seed.id.replace('thread-', ''), title: seed.title, status: seed.status },
      counterpart: { id: counterpartId, displayName: iAmBuyer ? seed.shop : seed.buyer },
      lastMessage: null,
      unreadCount: seed.unread,
      isLocked: seed.locked ?? false,
      createdAt: messages[0]?.createdAt ?? new Date(now).toISOString(),
    },
    messages,
  }
}

export class MockChat {
  private threads = new Map<string, ChatThreadSummary>()
  private messages = new Map<string, ChatMessage[]>()
  private sequence = 0

  constructor(private readonly role: UserRole, private readonly myId: string) {
    const now = Date.now()
    for (const seed of SEEDS) {
      const { summary, messages } = buildThread(seed, role, myId, now)
      this.threads.set(summary.id, summary)
      this.messages.set(summary.id, messages)
    }
  }

  private summarize(thread: ChatThreadSummary): ChatThreadSummary {
    const last = this.messages.get(thread.id)?.at(-1)
    return {
      ...thread,
      lastMessage: last ? { preview: (last.body ?? '').slice(0, 100), createdAt: last.createdAt, isMine: last.isMine } : null,
    }
  }

  /** A thread opened by a response given in the mock merchant feed (`thread-<postId>`) is created on first use. */
  private ensure(threadId: string): ChatThreadSummary | undefined {
    const known = this.threads.get(threadId)
    if (known || !threadId.startsWith('thread-')) return known

    const post = buildMockMerchantFeed().find(request => request.id === threadId.slice('thread-'.length))
    if (!post) return undefined

    const thread: ChatThreadSummary = {
      id: threadId,
      post: { id: post.id, title: post.title, status: post.status },
      counterpart: { id: `counterpart-${threadId}`, displayName: this.role === 'Buyer' ? 'Audio Shop Kraków' : post.buyerName },
      lastMessage: null,
      unreadCount: 0,
      isLocked: false,
      createdAt: new Date().toISOString(),
    }
    this.threads.set(threadId, thread)
    this.messages.set(threadId, [])
    return thread
  }

  /** Delivers the counterpart's messages queued by a test (see the hooks above). */
  private deliverIncoming() {
    if (!import.meta.client) return

    let queued: Array<{ threadId: string, body: string | null, attachmentUrl?: string }>
    try {
      queued = JSON.parse(sessionStorage.getItem('gk:mock-chat-incoming') ?? '[]')
      sessionStorage.removeItem('gk:mock-chat-incoming')
    }
    catch {
      return
    }

    for (const { threadId, body, attachmentUrl } of queued) {
      const thread = this.ensure(threadId)
      if (!thread) continue

      this.append(threadId, body, false, attachmentUrl ?? null)
      thread.unreadCount += 1
    }
  }

  private append(threadId: string, body: string | null, mine: boolean, attachmentUrl: string | null = null): ChatMessage {
    const message: ChatMessage = {
      id: `${threadId}-n${String(++this.sequence).padStart(4, '0')}`,
      threadId,
      senderId: mine ? this.myId : `counterpart-${threadId}`,
      isMine: mine,
      body,
      attachmentUrl,
      createdAt: new Date().toISOString(),
    }
    this.messages.get(threadId)!.push(message)
    return message
  }

  private notFound(): never {
    throw Object.assign(new Error('Conversation not found'), { statusCode: 404 })
  }

  listThreads(cursor: string | null, limit: number = THREADS_PAGE_SIZE): ChatThreadPage {
    this.deliverIncoming()

    const sorted = [...this.threads.values()]
      .map(thread => this.summarize(thread))
      .sort((a, b) => Date.parse(b.lastMessage?.createdAt ?? b.createdAt) - Date.parse(a.lastMessage?.createdAt ?? a.createdAt))

    const start = cursor === null ? 0 : Number(cursor)
    const end = start + limit
    return { items: sorted.slice(start, end), nextCursor: end < sorted.length ? String(end) : null }
  }

  getThread(threadId: string): ChatThreadSummary {
    this.deliverIncoming()

    const thread = this.ensure(threadId)
    return thread ? this.summarize(thread) : this.notFound()
  }

  getMessages(threadId: string, query: { before?: string, after?: string, limit?: number } = {}): ChatMessagePage {
    this.deliverIncoming()
    if (!this.ensure(threadId)) this.notFound()

    const all = this.messages.get(threadId)!
    const limit = query.limit ?? MESSAGES_PAGE_SIZE

    if (query.after) {
      const index = all.findIndex(message => message.id === query.after)
      return { items: all.slice(index + 1, index + 1 + limit), hasMore: false }
    }

    if (query.before) {
      const end = all.findIndex(message => message.id === query.before)
      const start = Math.max(0, end - limit)
      return { items: all.slice(start, end), hasMore: start > 0 }
    }

    return { items: all.slice(-limit), hasMore: all.length > limit }
  }

  sendMessage(threadId: string, body: string, image: File | null = null): ChatMessage {
    const thread = this.ensure(threadId)
    if (!thread) this.notFound()

    if (import.meta.client && sessionStorage.getItem('gk:mock-chat-fail-send')) {
      throw Object.assign(new Error('Unavailable'), { statusCode: 500 })
    }
    if (thread.isLocked) {
      throw Object.assign(new Error('thread_locked'), { statusCode: 403, data: { code: 'thread_locked' } })
    }

    if (image) {
      const problem = attachmentProblem(image)
      if (problem === 'too_large') {
        throw Object.assign(new Error('attachment_too_large'), { statusCode: 413, data: { code: 'attachment_too_large' } })
      }
      if (problem) {
        throw Object.assign(new Error('unsupported_attachment_type'), { statusCode: 415, data: { code: 'unsupported_attachment_type' } })
      }
    }

    return this.append(threadId, body || null, true, image ? URL.createObjectURL(image) : null)
  }

  markRead(threadId: string): void {
    const thread = this.ensure(threadId)
    if (!thread) this.notFound()
    thread.unreadCount = 0
  }

  unreadCount(): number {
    this.deliverIncoming()
    return [...this.threads.values()].reduce((sum, thread) => sum + thread.unreadCount, 0)
  }
}

const sessions = new Map<string, MockChat>()

/** One mock conversation set per signed-in account, kept for the life of the page. */
export function mockChatFor(role: UserRole, userId: string): MockChat {
  const key = `${role}:${userId}`
  let chat = sessions.get(key)
  if (!chat) {
    chat = new MockChat(role, userId)
    sessions.set(key, chat)
  }
  return chat
}
