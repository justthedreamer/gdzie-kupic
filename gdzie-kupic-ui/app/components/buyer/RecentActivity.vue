<script setup lang="ts">
import type { ActivityState, BuyerActivityEvent } from '~/utils/buyerHome'

defineProps<{ events: BuyerActivityEvent[] }>()

const { locale } = useI18n()

const STATE_ICON: Record<ActivityState, { icon: string, color: string }> = {
  Have: { icon: 'i-heroicons-check-circle', color: 'text-success' },
  Checking: { icon: 'i-heroicons-clock', color: 'text-warning' },
  Cannot: { icon: 'i-heroicons-x-circle', color: 'text-error' },
}
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-base font-semibold text-highlighted">
          {{ $t('buyer_home.activity.title') }}
        </h2>
        <!-- The full activity view does not exist yet. -->
        <UButton
          v-if="events.length"
          variant="link"
          size="sm"
          disabled
          class="px-0"
        >
          {{ $t('buyer_home.view_all') }}
        </UButton>
      </div>
    </template>

    <p v-if="!events.length" class="py-4 text-center text-sm text-muted">
      {{ $t('buyer_home.activity.empty') }}
    </p>

    <ul v-else class="divide-y divide-default">
      <li v-for="event in events" :key="event.id" class="flex items-center gap-3 py-3">
        <UIcon
          :name="STATE_ICON[event.state].icon"
          class="size-6 shrink-0"
          :class="STATE_ICON[event.state].color"
        />
        <div class="min-w-0 flex-1">
          <p class="truncate text-sm font-medium text-highlighted">
            {{ event.merchantName }}
          </p>
          <p class="text-sm text-muted">
            {{ $t(`buyer_home.activity.${event.state}`) }}
          </p>
        </div>
        <time :datetime="event.occurredAt" class="shrink-0 text-xs text-muted">
          {{ formatRelativeTime(event.occurredAt, locale) }}
        </time>
      </li>
    </ul>
  </UCard>
</template>
