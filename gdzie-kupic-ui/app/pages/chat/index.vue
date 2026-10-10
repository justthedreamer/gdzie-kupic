<script setup lang="ts">
import { INBOX_REFRESH_MS } from '~/utils/chat'

definePageMeta({
  layout: 'buyer',
  middleware: ['auth', 'role', 'chat-layout'],
  roles: ['Buyer', 'Merchant'],
  shellTitleKey: 'chat.title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('chat.title')} | Gdzie Kupić` })

const chatStore = useChatStore()
const authStore = useAuthStore()

// The list is shown from the store at once and refreshed on entry.
onMounted(() => chatStore.refreshThreads())
usePolling(() => chatStore.refreshThreads(), INBOX_REFRESH_MS)

const isLoading = computed(() => chatStore.status === 'idle' || chatStore.status === 'pending')
const hasError = computed(() => chatStore.status === 'error')
const emptyKey = computed(() => (authStore.user?.role === 'Merchant' ? 'chat.empty_merchant' : 'chat.empty_buyer'))

const sentinel = ref<HTMLElement | null>(null)
useInfiniteScroll(sentinel, () => chatStore.loadMoreThreads(), { refresh: () => chatStore.threads.length })
</script>

<template>
  <div class="container mx-auto max-w-3xl space-y-4 px-4 py-6 lg:py-8">
    <h1 class="hidden text-2xl font-bold text-highlighted lg:block">
      {{ $t('chat.title') }}
    </h1>

    <p v-if="isLoading && !chatStore.threads.length" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="hasError" class="space-y-3">
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

    <UCard v-else-if="!chatStore.threads.length">
      <div class="space-y-3 py-8 text-center" data-testid="chat-empty">
        <UIcon name="i-heroicons-chat-bubble-left-right" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ $t('chat.empty_title') }}
        </p>
        <p class="text-sm text-muted">
          {{ $t(emptyKey) }}
        </p>
      </div>
    </UCard>

    <template v-else>
      <ul :aria-label="$t('chat.inbox_label')" class="space-y-2">
        <li v-for="thread in chatStore.threads" :key="thread.id">
          <ChatThreadRow :thread="thread" />
        </li>
      </ul>

      <!-- The sentinel loads the next page when it scrolls into view; the button is the manual way (and the retry). -->
      <div
        v-if="chatStore.nextCursor"
        ref="sentinel"
        class="flex flex-col items-center gap-2 py-2"
        data-testid="chat-more"
      >
        <p v-if="chatStore.loadMoreFailed" class="text-sm text-error">
          {{ $t('chat.load_more_error') }}
        </p>
        <UButton
          color="neutral"
          variant="outline"
          :loading="chatStore.loadingMore"
          @click="chatStore.loadMoreThreads()"
        >
          {{ chatStore.loadMoreFailed ? $t('chat.retry') : $t('chat.load_more') }}
        </UButton>
      </div>
    </template>
  </div>
</template>
