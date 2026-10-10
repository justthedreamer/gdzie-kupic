<script setup lang="ts">
import type { LiveCounts } from '~/utils/buyerHome'

const props = defineProps<{ request: LiveCounts }>()

const breakdown = computed(() => statusBreakdown(props.request))

// With the split (real status) the positive answers get a row each, otherwise one "have" row.
const positiveRows = computed(() => {
  const { positive, have } = breakdown.value

  return positive
    ? [
        { key: 'have', dot: 'bg-success', count: positive.haveIt },
        { key: 'may_have', dot: 'bg-success/60', count: positive.mayHaveIt },
        { key: 'can_order', dot: 'bg-info', count: positive.canOrderIt },
      ]
    : [{ key: 'have', dot: 'bg-success', count: have }]
})

const rows = computed(() => [
  { key: 'checking', dot: 'bg-warning', count: breakdown.value.checking },
  ...positiveRows.value,
  { key: 'cannot', dot: 'bg-error', count: breakdown.value.cannot },
  { key: 'none', dot: 'bg-neutral-400', count: breakdown.value.none },
])
</script>

<template>
  <UCard>
    <template #header>
      <div class="flex items-center justify-between">
        <h2 class="text-base font-semibold text-highlighted">
          {{ $t('buyer_home.live_status') }}
        </h2>
        <UBadge v-if="request.isLive" color="success" variant="subtle">
          {{ $t('buyer_home.live') }}
        </UBadge>
        <UBadge v-else color="neutral" variant="subtle">
          {{ $t('buyer_home.closed') }}
        </UBadge>
      </div>
    </template>

    <p class="text-4xl font-bold text-highlighted" data-testid="notified-count">
      {{ breakdown.notified }}
    </p>
    <p class="text-sm text-muted">
      {{ $t('buyer_home.merchants_notified') }}
    </p>

    <ul class="mt-4 space-y-2">
      <li v-for="row in rows" :key="row.key" class="flex items-center gap-2 text-sm" data-testid="status-row" :data-key="row.key">
        <span class="size-2.5 rounded-full" :class="row.dot" aria-hidden="true" />
        <span class="flex-1 text-muted">{{ $t(`buyer_home.status.${row.key}`) }}</span>
        <span class="font-semibold text-highlighted">{{ row.count }}</span>
      </li>
    </ul>
  </UCard>
</template>
