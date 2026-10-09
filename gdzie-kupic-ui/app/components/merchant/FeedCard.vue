<script setup lang="ts">
import { formatBudget, formatDistance, type MerchantFeedRequest, type MerchantResponse } from '~/utils/merchantFeed'

const props = defineProps<{ request: MerchantFeedRequest }>()

defineEmits<{ respond: [state: MerchantResponse] }>()

const { locale } = useI18n()

const posted = computed(() => formatRelativeTime(props.request.postedAt, locale.value))
const budget = computed(() => (props.request.budget === null ? null : formatBudget(props.request.budget, locale.value)))
const category = computed(() => [props.request.category, props.request.tag].filter(Boolean).join(' · '))

const RESPONSE_COLOR = { HaveIt: 'success', MayHaveIt: 'warning', CantHelp: 'error' } as const
</script>

<template>
  <UCard
    :class="{ 'opacity-70': request.myResponse === 'CantHelp' }"
    :ui="{ body: 'space-y-3' }"
    data-testid="feed-card"
  >
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0 space-y-1">
        <h2 class="text-base font-semibold text-highlighted">
          <NuxtLink :to="`/feed/${request.id}`" class="hover:text-primary hover:underline">
            {{ request.title }}
          </NuxtLink>
        </h2>
        <div class="flex flex-wrap items-center gap-2">
          <UBadge v-if="request.isUrgent" color="error" variant="subtle">
            {{ $t('merchant_feed.urgent') }}
          </UBadge>
          <UBadge v-if="request.myResponse === null" color="primary" variant="subtle">
            {{ $t('merchant_feed.new') }}
          </UBadge>
          <UBadge v-else :color="RESPONSE_COLOR[request.myResponse]" variant="subtle">
            {{ $t(`merchant_feed.response.${request.myResponse}`) }}
          </UBadge>
        </div>
      </div>
      <span class="shrink-0 text-xs text-muted">{{ posted }}</span>
    </div>

    <p v-if="request.description" class="line-clamp-2 text-sm text-muted">
      {{ request.description }}
    </p>

    <ul class="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
      <li class="flex items-center gap-1.5">
        <UIcon name="i-heroicons-map-pin" class="size-4 shrink-0" />
        {{ request.city }} · {{ formatDistance(request.distanceKm, locale) }}
      </li>
      <li v-if="budget" class="flex items-center gap-1.5">
        <UIcon name="i-heroicons-banknotes" class="size-4 shrink-0" />
        {{ $t('merchant_feed.budget_up_to', { amount: budget }) }}
      </li>
      <li class="flex items-center gap-1.5">
        <UIcon name="i-heroicons-tag" class="size-4 shrink-0" />
        {{ category }}
      </li>
    </ul>

    <MerchantResponseButtons :current="request.myResponse" @select="$emit('respond', $event)" />
  </UCard>
</template>
