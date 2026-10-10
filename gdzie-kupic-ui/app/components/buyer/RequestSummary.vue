<script setup lang="ts">
import type { BuyerRequestSummary } from '~/utils/buyerHome'

const props = defineProps<{ request: BuyerRequestSummary }>()

const { locale } = useI18n()

const posted = computed(() => formatRelativeTime(props.request.postedAt, locale.value))
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
        <UIcon name="i-heroicons-arrows-pointing-out" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.radius_km') }}</dt>
        <dd>
          {{ request.radiusKm === null ? $t('request.form.radius_unlimited') : $t('buyer_home.radius', { km: request.radiusKm }) }}
        </dd>
      </div>
      <div class="flex items-center gap-2">
        <UIcon name="i-heroicons-tag" class="size-5 shrink-0 text-muted" />
        <dt class="sr-only">{{ $t('request.category') }}</dt>
        <dd>{{ category }}</dd>
      </div>
    </dl>

    <template #footer>
      <UButton
        :to="`/requests/${request.id}`"
        variant="link"
        color="primary"
        trailing-icon="i-heroicons-arrow-right"
        class="px-0"
      >
        {{ $t('buyer_home.view_details') }}
      </UButton>
    </template>
  </UCard>
</template>

