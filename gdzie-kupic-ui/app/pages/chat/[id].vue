<script setup lang="ts">
import { CHAT_PATH, POLL_INTERVAL_MS, isNearEnd, type AttachmentProblem } from '~/utils/chat'

definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role', 'chat-layout'],
  roles: ['Buyer', 'Merchant'],
  shellTitleKey: 'chat.title',
})

const { t } = useI18n()
const route = useRoute()
const chatStore = useChatStore()
const authStore = useAuthStore()

const threadId = computed(() => String(route.params.id))
const conversation = computed(() => chatStore.conversations[threadId.value])
const status = computed(() => conversation.value?.status ?? 'pending')
const thread = computed(() => conversation.value?.thread ?? null)
const messages = computed(() => conversation.value?.messages ?? [])
const pending = computed(() => conversation.value?.pending ?? [])

useSeoMeta({ title: () => `${thread.value?.counterpart.displayName ?? t('chat.title')} | Gdzie Kupić` })

const requestLink = computed(() => {
  if (!thread.value) return null
  return authStore.user?.role === 'Merchant' ? `/feed/${thread.value.post.id}` : `/requests/${thread.value.post.id}`
})

// ─── Loading and polling ────────────────────────────────────────────────────
const list = ref<HTMLElement | null>(null)
// False until the first scroll to the newest message: nothing may load "older" before that.
const ready = ref(false)
const hasNew = ref(false)

function scrollToEnd() {
  const el = list.value
  if (el) el.scrollTop = el.scrollHeight
  hasNew.value = false
}

async function open() {
  await chatStore.open(threadId.value)
  await nextTick()
  scrollToEnd()
  ready.value = true
}
onMounted(open)

usePolling(() => (ready.value ? chatStore.poll(threadId.value) : undefined), POLL_INTERVAL_MS)

// ─── Scroll behaviour ───────────────────────────────────────────────────────
// Older messages are prepended without moving what is on screen; a new message scrolls
// the list to the end when you are there already (or wrote it yourself), otherwise it
// offers a "new messages" button.
watch(
  () => ({ first: messages.value[0]?.id, last: messages.value.at(-1)?.id, pending: pending.value.length }),
  async (now, before) => {
    const el = list.value
    if (!el || !ready.value) return

    const previousHeight = el.scrollHeight
    const wasNearEnd = isNearEnd(el)
    await nextTick()

    if (now.last === before.last && now.pending <= before.pending) {
      if (now.first !== before.first) el.scrollTop += el.scrollHeight - previousHeight
      return
    }

    const own = now.pending > before.pending || messages.value.at(-1)?.isMine === true
    if (wasNearEnd || own) scrollToEnd()
    else hasNew.value = true
  },
  { flush: 'pre' },
)

function onScroll() {
  if (list.value && isNearEnd(list.value)) hasNew.value = false
}

const olderSentinel = ref<HTMLElement | null>(null)
useInfiniteScroll(
  olderSentinel,
  () => {
    // Only when the reader is at the top: the first report may come from a layout that is out of date.
    if (ready.value && (list.value?.scrollTop ?? 0) < 200) void chatStore.loadOlder(threadId.value)
  },
  { refresh: () => messages.value.length },
)

// ─── Sending ────────────────────────────────────────────────────────────────
const text = ref('')
const image = ref<File | null>(null)
const refused = ref<AttachmentProblem | null>(null)

async function send() {
  const body = text.value
  const file = image.value
  text.value = ''
  image.value = null
  refused.value = null

  const result = await chatStore.send(threadId.value, body, file)
  if (result === 'invalid' || result === 'too_large' || result === 'unsupported_type') {
    // Nothing was sent: the writer gets the text and the picture back.
    text.value = body
    image.value = file
    if (result !== 'invalid') refused.value = result
  }
}
</script>

<template>
  <div class="mx-auto -mb-6 flex h-[calc(100dvh-8rem)] max-w-3xl flex-col bg-default lg:mb-0 lg:h-dvh" data-testid="chat-thread">
    <header class="flex items-center gap-2 border-b border-default px-3 py-2.5">
      <UButton
        :to="CHAT_PATH"
        icon="i-heroicons-arrow-left"
        variant="ghost"
        color="neutral"
        :aria-label="$t('chat.back_to_inbox')"
        data-testid="chat-back"
      />
      <div v-if="thread" class="min-w-0 flex-1">
        <h1 class="truncate text-base font-semibold text-highlighted" data-testid="chat-counterpart">
          {{ thread.counterpart.displayName }}
        </h1>
        <p class="truncate text-xs text-muted">
          <NuxtLink v-if="requestLink" :to="requestLink" class="hover:underline" data-testid="chat-request-link">
            {{ $t('chat.open_request', { title: thread.post.title }) }}
          </NuxtLink>
        </p>
      </div>
      <UBadge v-if="thread?.isLocked" color="neutral" variant="subtle" size="sm" icon="i-heroicons-lock-closed">
        {{ $t('chat.locked') }}
      </UBadge>
    </header>

    <div v-if="status === 'pending'" class="p-4 text-sm text-muted">
      {{ $t('common.loading') }}
    </div>

    <div v-else-if="status === 'notFound'" class="p-4" data-testid="chat-not-found">
      <UAlert color="warning" variant="subtle" icon="i-heroicons-exclamation-triangle" :description="$t('chat.thread_not_found')" />
    </div>

    <div v-else-if="status === 'error'" class="space-y-3 p-4">
      <UAlert color="error" variant="subtle" icon="i-heroicons-exclamation-circle" :description="$t('chat.thread_load_error')" />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="open()">
        {{ $t('chat.retry') }}
      </UButton>
    </div>

    <template v-else-if="thread">
      <p
        v-if="thread.post.status !== 'Active'"
        class="border-b border-default bg-muted px-4 py-2 text-xs text-muted"
        data-testid="chat-request-inactive"
      >
        {{ $t('chat.request_inactive', { status: $t(`request.status.${thread.post.status}`) }) }}
      </p>

      <div class="relative min-h-0 flex-1">
        <div
          ref="list"
          class="h-full overflow-y-auto px-3 py-4 [overflow-anchor:none]"
          role="log"
          :aria-label="$t('chat.messages_label')"
          data-testid="chat-messages"
          @scroll.passive="onScroll"
        >
          <div v-if="conversation?.hasMore" ref="olderSentinel" class="flex flex-col items-center gap-1 pb-3" data-testid="chat-older">
            <p v-if="conversation.olderFailed" class="text-xs text-error">
              {{ $t('chat.load_older_error') }}
            </p>
            <UButton
              size="xs"
              color="neutral"
              variant="outline"
              :loading="conversation.loadingOlder"
              @click="chatStore.loadOlder(threadId)"
            >
              {{ conversation.olderFailed ? $t('chat.retry') : $t('chat.load_older') }}
            </UButton>
          </div>

          <p v-if="!messages.length && !pending.length" class="py-8 text-center text-sm text-muted" data-testid="chat-empty-thread">
            {{ $t('chat.empty_thread') }}
          </p>

          <ul class="space-y-2">
            <ChatMessageBubble
              v-for="message in messages"
              :key="message.id"
              :own="message.isMine"
              :body="message.body"
              :attachment-url="message.attachmentUrl"
              :created-at="message.createdAt"
              :author="thread.counterpart.displayName"
              @reload-image="chatStore.refreshMessages(threadId)"
            />
            <ChatMessageBubble
              v-for="item in pending"
              :key="item.localId"
              own
              :body="item.body"
              :attachment-url="item.previewUrl"
              :created-at="item.createdAt"
              :author="thread.counterpart.displayName"
              :state="item.status"
              @retry="chatStore.retry(threadId, item.localId)"
              @discard="chatStore.discard(threadId, item.localId)"
            />
          </ul>
        </div>

        <UButton
          v-if="hasNew"
          class="absolute bottom-3 left-1/2 -translate-x-1/2 shadow-lg"
          size="sm"
          icon="i-heroicons-arrow-down"
          data-testid="chat-new-messages"
          @click="scrollToEnd()"
        >
          {{ $t('chat.new_messages') }}
        </UButton>
      </div>

      <ChatComposer v-model="text" v-model:image="image" v-model:refused="refused" :locked="thread.isLocked" @send="send" />
    </template>
  </div>
</template>
