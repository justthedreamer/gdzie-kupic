<script setup lang="ts">
import { formatMessageTime, threadPath, type ChatThreadSummary } from '~/utils/chat'

// One row of the inbox: who, which request, the last message and the unread count.
const props = defineProps<{ thread: ChatThreadSummary }>()

const { locale } = useI18n()
const time = computed(() =>
  props.thread.lastMessage ? formatMessageTime(props.thread.lastMessage.createdAt, locale.value) : '',
)
</script>

<template>
  <NuxtLink
    :to="threadPath(thread.id)"
    class="flex items-start gap-3 rounded-lg border border-default bg-default p-4 transition hover:bg-elevated focus-visible:outline-2 focus-visible:outline-primary"
    data-testid="chat-thread-row"
  >
    <UAvatar :alt="thread.counterpart.displayName" size="md" class="shrink-0" />

    <div class="min-w-0 flex-1 space-y-0.5">
      <div class="flex items-baseline justify-between gap-2">
        <p class="truncate font-semibold text-highlighted" :class="{ 'font-bold': thread.unreadCount > 0 }">
          {{ thread.counterpart.displayName }}
        </p>
        <time v-if="time" :datetime="thread.lastMessage?.createdAt" class="shrink-0 text-xs text-muted">
          {{ time }}
        </time>
      </div>

      <p class="truncate text-sm text-toned">
        {{ thread.post.title }}
      </p>

      <p class="truncate text-sm" :class="thread.unreadCount > 0 ? 'text-highlighted' : 'text-muted'">
        <template v-if="thread.lastMessage">
          <span v-if="thread.lastMessage.isMine">{{ $t('chat.you') }} </span>{{ thread.lastMessage.preview }}
        </template>
        <template v-else>
          {{ $t('chat.no_messages') }}
        </template>
      </p>
    </div>

    <div class="flex shrink-0 flex-col items-end gap-1">
      <UBadge
        v-if="thread.unreadCount > 0"
        color="primary"
        variant="solid"
        size="sm"
        :aria-label="$t('chat.unread_aria', { count: thread.unreadCount })"
        data-testid="chat-thread-unread"
      >
        {{ formatBadgeCount(thread.unreadCount) }}
      </UBadge>
      <UBadge v-if="thread.isLocked" color="neutral" variant="subtle" size="sm" icon="i-heroicons-lock-closed">
        {{ $t('chat.locked') }}
      </UBadge>
    </div>
  </NuxtLink>
</template>
