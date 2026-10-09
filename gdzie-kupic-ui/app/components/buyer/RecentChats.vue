<script setup lang="ts">
import type { BuyerChatPreview } from '~/utils/buyerHome'

defineProps<{ chats: BuyerChatPreview[] }>()

const { locale } = useI18n()
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="text-base font-semibold text-highlighted">
        {{ $t('buyer_home.chats.title') }}
      </h2>
    </template>

    <p v-if="!chats.length" class="py-4 text-center text-sm text-muted">
      {{ $t('buyer_home.chats.empty') }}
    </p>

    <!-- Chat threads arrive in Phase 5; until then the rows are not links. -->
    <ul v-else class="divide-y divide-default">
      <li v-for="chat in chats" :key="chat.id" class="flex items-center gap-3 py-3">
        <UAvatar :text="chat.merchantName.charAt(0)" size="md" />
        <div class="min-w-0 flex-1">
          <p class="truncate text-sm font-medium text-highlighted">
            {{ chat.merchantName }}
          </p>
          <p class="truncate text-sm text-muted">
            {{ chat.lastMessage }}
          </p>
        </div>
        <time :datetime="chat.sentAt" class="shrink-0 text-xs text-muted">
          {{ formatRelativeTime(chat.sentAt, locale) }}
        </time>
      </li>
    </ul>
  </UCard>
</template>
