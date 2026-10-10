<script setup lang="ts">
import { CHAT_PATH, INBOX_REFRESH_MS } from '~/utils/chat'

// The latest conversations of the buyer, from the real chat threads (most recent activity
// first). Refreshed on the inbox schedule while the home page is open.
const LIMIT = 4

const chatStore = useChatStore()

onMounted(() => chatStore.refreshThreads())
usePolling(() => chatStore.refreshThreads(), INBOX_REFRESH_MS)

const threads = computed(() => chatStore.threads.slice(0, LIMIT))
const isLoading = computed(() => chatStore.status === 'idle' || chatStore.status === 'pending')
const hasError = computed(() => chatStore.status === 'error')
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between gap-2">
        <h2 class="text-base font-semibold text-highlighted">
          {{ $t('buyer_home.chats.title') }}
        </h2>
        <UButton v-if="threads.length" :to="CHAT_PATH" variant="link" color="neutral" size="xs" class="px-0" data-testid="chats-view-all">
          {{ $t('buyer_home.chats.view_all') }}
        </UButton>
      </div>
    </template>

    <p v-if="isLoading && !threads.length" class="py-4 text-center text-sm text-muted" data-testid="chats-loading">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="hasError && !threads.length" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('chat.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="chatStore.loadThreads()">
        {{ $t('chat.retry') }}
      </UButton>
    </div>

    <p v-else-if="!threads.length" class="py-4 text-center text-sm text-muted" data-testid="chats-empty">
      {{ $t('buyer_home.chats.empty') }}
    </p>

    <ul v-else class="space-y-2">
      <li v-for="thread in threads" :key="thread.id">
        <ChatThreadRow :thread="thread" />
      </li>
    </ul>
  </UCard>
</template>
