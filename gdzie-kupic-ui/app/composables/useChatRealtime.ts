import { postIdInPath, threadIdInPath } from '~/utils/chat'

/**
 * Keeps the chat inbox, the unread badge and the in-app notifications current from the
 * server's events, on every page of the shell (called once by `shell/Frame.vue`). The events
 * carry identifiers only: the store always refetches over REST.
 *
 * - `messageReceived`: the inbox and the badge refresh, except for the thread the user has
 *   open: that page fetches the message and marks it read itself (a refresh here would flash
 *   it as unread).
 * - `threadUpdated`: the inbox refreshes (a new thread, a locked one).
 * - `notificationRaised`: a toast with a link, and the badges, unless the user is already
 *   looking at that thread or that request.
 * - `resync`: the inbox and the badge catch up after a reconnect.
 */
export function useChatRealtime(reloadBadges: () => void) {
  const chatStore = useChatStore()
  const route = useRoute()
  const router = useRouter()
  const toast = useToast()
  const { t } = useI18n()

  const openThreadId = () => threadIdInPath(route.path)

  useRealtimeEvent('messageReceived', (event) => {
    if (event.threadId !== openThreadId()) void chatStore.refreshInbox()
  })

  useRealtimeEvent('threadUpdated', () => void chatStore.refreshInbox())

  useRealtimeEvent('notificationRaised', (event) => {
    const watching = (event.threadId !== null && event.threadId === openThreadId())
      || (event.postId !== null && event.postId === postIdInPath(route.path))
    if (watching) return

    reloadBadges()

    const isMessage = event.kind === 'newMessage'
    const link = isMessage && event.threadId
      ? `/chat/${encodeURIComponent(event.threadId)}`
      : event.postId ? `/requests/${encodeURIComponent(event.postId)}` : null

    toast.add({
      title: t(isMessage ? 'notification.new_message' : 'notification.merchant_responded'),
      icon: isMessage ? 'i-heroicons-chat-bubble-left-right' : 'i-heroicons-check-circle',
      actions: link
        ? [{
            label: t(isMessage ? 'notification.open_chat' : 'notification.open_request'),
            color: 'neutral',
            variant: 'outline',
            onClick: () => void router.push(link),
          }]
        : [],
    })
  })

  useRealtimeEvent('resync', () => void chatStore.refreshInbox())
}
