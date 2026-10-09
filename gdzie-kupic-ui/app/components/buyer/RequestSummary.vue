<script setup lang="ts">
import type { BuyerRequestSummary } from '~/utils/buyerHome'

const props = defineProps<{ request: BuyerRequestSummary }>()

const { locale } = useI18n()

const posted = computed(() => formatRelativeTime(props.request.postedAt, locale.value))
const budget = computed(() =>
  props.request.budget === null
    ? null
    : new Intl.NumberFormat(locale.value, { style: 'currency', currency: 'PLN', maximumFractionDigits: 0 }).format(props.request.budget),
)
const category = computed(() => [props.request.category, props.request.tag].filter(Boolean).join(' · '))
</script>

<template>
  <UCard>
    <div class="flex flex-wrap items-start justify-between gap-3">
      <h2 class="text-lg font-semibold text-highlighted">
        {{ request.title }}
      </h2>
      <UBadge color="neutral" variant="subtle">
        {{ $t('buyer_home.posted', { time: posted }) }}
      </UBadge>
    </div>

    <p v-if="request.description" class="mt-2 text-sm text-muted">
      {{ request.description }}
    </p>

    <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-map-pin" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.location') }}</dt>
        <dd>{{ request.city }}, {{ request.country }}</dd>
      </div>
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-arrows-pointing-out" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.radius_km') }}</dt>
        <dd>{{ $t('buyer_home.radius', { km: request.radiusKm }) }}</dd>
      </div>
      <div v-if="budget" class="flex items-center gap-2">
        <UIcon name="i-heroicons-banknotes" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.budget') }}</dt>
        <dd>{{ budget }}</dd>
      </div>
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-tag" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.category') }}</dt>
        <dd>{{ category }}</dd>
      </div>
    </dl>

    <template #footer>
      <!-- The request detail page arrives in Phase 4; until then the action is inert. -->
      <UButton
        variant="link"
        color="primary"
        trailing-icon="i-heroicons-arrow-right"
        disabled
        class="px-0"
      >
        {{ $t('buyer_home.view_details') }}
      </UButton>
    </template>
  </UCard>
</template>
