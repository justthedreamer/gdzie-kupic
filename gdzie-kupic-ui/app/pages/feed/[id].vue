<script setup lang="ts">
import { formatBudget, formatDistance, RESPONSE_COLOR } from '~/utils/merchantFeed'

definePageMeta({
  layout: 'merchant',
  middleware: ['auth', 'role'],
  roles: ['Merchant'],
  shellTitleKey: 'merchant_feed.details_title',
})

const route = useRoute()
const { t, locale } = useI18n()

const feedStore = useMerchantFeedStore()
onMounted(() => feedStore.load())

const request = computed(() => feedStore.byId(String(route.params.id)))
const isLoading = computed(() => feedStore.status === 'idle' || feedStore.status === 'pending')

useSeoMeta({ title: () => `${request.value?.title ?? t('merchant_feed.details_title')} | Gdzie Kupić` })

const posted = computed(() => (request.value ? formatRelativeTime(request.value.postedAt, locale.value) : ''))
const budget = computed(() =>
  request.value?.budget == null ? null : formatBudget(request.value.budget, locale.value),
)
const category = computed(() => [request.value?.category, request.value?.tag].filter(Boolean).join(' · '))
const deadline = computed(() =>
  request.value?.deadline
    ? new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(request.value.deadline))
    : null,
)
</script>

<template>
  <div class="container mx-auto max-w-5xl space-y-6 px-4 py-6 lg:py-8">
    <UButton
      to="/feed"
      class="-ml-2.5 hidden lg:inline-flex"
      color="neutral"
      variant="ghost"
      icon="i-heroicons-arrow-left"
    >
      {{ $t('merchant_feed.back_to_feed') }}
    </UButton>

    <p v-if="isLoading && !request" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <UCard v-else-if="!request">
      <div class="space-y-3 py-8 text-center">
        <UIcon name="i-heroicons-question-mark-circle" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ $t('merchant_feed.not_found') }}
        </p>
        <UButton to="/feed" variant="outline">
          {{ $t('merchant_feed.back_to_feed') }}
        </UButton>
      </div>
    </UCard>

    <template v-else>
      <div class="grid gap-6 lg:grid-cols-3">
        <div class="space-y-6 lg:col-span-2">
          <UCard>
            <div class="flex flex-wrap items-start justify-between gap-3">
              <h1 class="text-xl font-semibold text-highlighted">
                {{ request.title }}
              </h1>
              <div class="flex items-center gap-2">
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

            <dl class="mt-4 grid gap-4 text-sm sm:grid-cols-2">
              <div>
                <dt class="text-muted">
                  {{ $t('merchant_feed.buyer') }}
                </dt>
                <dd class="flex items-center gap-1.5 font-medium text-highlighted">
                  {{ request.buyerName }}
                  <UBadge v-if="request.buyerVerified" color="success" variant="subtle" size="sm" icon="i-heroicons-check-badge">
                    {{ $t('merchant_feed.verified') }}
                  </UBadge>
                </dd>
              </div>
              <div>
                <dt class="text-muted">
                  {{ $t('request.location') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ request.city }} · {{ $t('merchant_feed.km_from_you', { distance: formatDistance(request.distanceKm, locale) }) }}
                </dd>
              </div>
              <div v-if="budget">
                <dt class="text-muted">
                  {{ $t('request.budget') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ $t('merchant_feed.budget_up_to', { amount: budget }) }}
                </dd>
              </div>
              <div>
                <dt class="text-muted">
                  {{ $t('request.category') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ category }}
                </dd>
              </div>
              <div>
                <dt class="text-muted">
                  {{ $t('merchant_feed.posted') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ posted }}
                </dd>
              </div>
              <div v-if="deadline">
                <dt class="text-muted">
                  {{ $t('merchant_feed.deadline') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ deadline }}
                </dd>
              </div>
            </dl>
          </UCard>

          <UCard v-if="request.description">
            <h2 class="text-base font-semibold text-highlighted">
              {{ $t('merchant_feed.description') }}
            </h2>
            <p class="mt-2 text-sm text-muted">
              {{ request.description }}
            </p>
          </UCard>

          <UCard>
            <h2 class="text-base font-semibold text-highlighted">
              {{ $t('merchant_feed.buyer_location') }}
            </h2>
            <!-- Placeholder: there is no map component yet; shows the area as concentric rings. -->
            <div
              class="relative mt-3 flex h-40 items-center justify-center overflow-hidden rounded-lg bg-elevated"
              role="img"
              :aria-label="$t('merchant_feed.buyer_location_aria', { city: request.city, km: request.buyerRadiusKm })"
            >
              <span class="absolute size-32 rounded-full border-2 border-primary/20 bg-primary/5" />
              <span class="absolute size-20 rounded-full border-2 border-primary/40 bg-primary/10" />
              <UIcon name="i-heroicons-map-pin" class="relative size-8 text-primary" />
            </div>
            <p class="mt-2 text-sm text-muted">
              {{ request.city }} · {{ $t('merchant_feed.buyer_radius', { km: request.buyerRadiusKm }) }}
            </p>
          </UCard>

          <UCard>
            <h2 class="text-base font-semibold text-highlighted">
              {{ $t('merchant_feed.your_response') }}
            </h2>
            <p class="mt-1 mb-3 text-sm text-muted" data-testid="current-response">
              {{ request.myResponse ? $t(`merchant_feed.response.${request.myResponse}`) : $t('merchant_feed.no_response_yet') }}
            </p>
            <MerchantResponseButtons :current="request.myResponse" @select="feedStore.respond(request.id, $event)" />
          </UCard>
        </div>

        <RequestLiveStatus :request="request" />
      </div>
    </template>
  </div>
</template>
