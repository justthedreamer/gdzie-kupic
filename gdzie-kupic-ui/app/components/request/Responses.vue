<script setup lang="ts">
import type { PostResponse } from '~/composables/api/usePostsApi'
import { threadPath } from '~/utils/chat'

// The merchants that can help with a request, each with the chat thread their answer
// opened. "Can't help" merchants are not listed (they are only in the status counts).
defineProps<{
  responses: PostResponse[]
  /** The first load finished (successfully or not). */
  loaded: boolean
  failed: boolean
}>()

const emit = defineEmits<{ retry: [] }>()

const link = resolveComponent('NuxtLink')
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="text-base font-semibold text-highlighted">
        {{ $t('request.detail.responses_title') }}
      </h2>
    </template>

    <p v-if="!loaded" class="text-sm text-muted" data-testid="responses-loading">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="failed && !responses.length" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('request.detail.responses_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="emit('retry')">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <p v-else-if="!responses.length" class="text-sm text-muted" data-testid="responses-empty">
      {{ $t('request.detail.responses_empty') }}
    </p>

    <template v-else>
      <ul class="divide-y divide-default" data-testid="response-list">
        <li v-for="response in responses" :key="response.merchantId" data-testid="response-row">
          <component
            :is="response.threadId ? link : 'div'"
            :to="response.threadId ? threadPath(response.threadId) : undefined"
            class="flex items-center gap-3 py-3"
            :class="response.threadId ? 'rounded-md transition hover:bg-elevated focus-visible:outline-2 focus-visible:outline-primary' : ''"
          >
            <UAvatar :alt="response.shopName" size="md" class="shrink-0" />

            <div class="min-w-0 flex-1 space-y-1">
              <p class="truncate text-sm font-semibold text-highlighted">
                {{ response.shopName }}
              </p>
              <UBadge :color="RESPONSE_STATE_COLOR[response.state]" variant="subtle" size="sm" data-testid="response-state">
                {{ $t(`request.response_state.${response.state}`) }}
              </UBadge>
            </div>

            <UBadge
              v-if="response.unreadCount > 0"
              color="primary"
              variant="solid"
              size="sm"
              :aria-label="$t('chat.unread_aria', { count: response.unreadCount })"
              data-testid="response-unread"
            >
              {{ formatBadgeCount(response.unreadCount) }}
            </UBadge>

            <span v-if="response.threadId" class="flex shrink-0 items-center gap-1 text-sm text-primary">
              <UIcon name="i-heroicons-chat-bubble-left-right" class="size-4" aria-hidden="true" />
              {{ $t('request.detail.open_chat') }}
            </span>
          </component>
        </li>
      </ul>

      <p v-if="failed" class="mt-2 text-xs text-muted">
        {{ $t('request.detail.status_stale') }}
      </p>
    </template>
  </UCard>
</template>
