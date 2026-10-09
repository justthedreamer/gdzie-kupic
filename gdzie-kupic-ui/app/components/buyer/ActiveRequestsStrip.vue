<script setup lang="ts">
import type { BuyerRequestSummary } from '~/utils/buyerHome'

defineProps<{
  requests: BuyerRequestSummary[]
  selectedId: string | null
}>()

const emit = defineEmits<{ select: [id: string] }>()

const scroller = ref<HTMLElement | null>(null)

function scrollBy(direction: 1 | -1) {
  scroller.value?.scrollBy({ left: direction * 280, behavior: 'smooth' })
}
</script>

<template>
  <section aria-labelledby="active-requests-title">
    <div class="mb-3 flex items-center justify-between">
      <h2 id="active-requests-title" class="text-base font-semibold text-highlighted">
        {{ $t('buyer_home.active_requests') }}
      </h2>
      <div class="hidden gap-1 lg:flex">
        <UButton
          icon="i-heroicons-chevron-left"
          variant="ghost"
          color="neutral"
          size="sm"
          :aria-label="$t('buyer_home.scroll_left')"
          @click="scrollBy(-1)"
        />
        <UButton
          icon="i-heroicons-chevron-right"
          variant="ghost"
          color="neutral"
          size="sm"
          :aria-label="$t('buyer_home.scroll_right')"
          @click="scrollBy(1)"
        />
      </div>
    </div>

    <ul ref="scroller" class="flex snap-x gap-3 overflow-x-auto pb-2">
      <li v-for="request in requests" :key="request.id" class="w-64 shrink-0 snap-start">
        <button
          type="button"
          :aria-pressed="request.id === selectedId"
          class="w-full rounded-xl border p-4 text-left transition-colors"
          :class="request.id === selectedId
            ? 'border-primary bg-primary/5 ring-1 ring-primary'
            : 'border-default bg-default hover:bg-elevated'"
          @click="emit('select', request.id)"
        >
          <span class="block truncate font-medium text-highlighted">{{ request.title }}</span>
          <span class="mt-1 flex items-center gap-1.5 text-sm">
            <template v-if="request.isLive">
              <span class="size-2 rounded-full bg-success" aria-hidden="true" />
              <span class="font-medium text-success">{{ $t('buyer_home.live') }}</span>
            </template>
            <span v-else class="text-muted">
              {{ $t('buyer_home.responses', respondedCount(request)) }}
            </span>
          </span>
        </button>
      </li>
    </ul>
  </section>
</template>
