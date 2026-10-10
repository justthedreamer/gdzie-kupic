<script setup lang="ts">
import { canRespond, formatDistance, RESPONSE_COLOR, threadPath, type MerchantResponse } from '~/utils/merchantFeed'

definePageMeta({
  layout: 'merchant',
  middleware: ['auth', 'role'],
  roles: ['Merchant'],
  shellTitleKey: 'merchant_feed.details_title',
})

const route = useRoute()
const { t, locale } = useI18n()

const feedStore = useMerchantFeedStore()

const id = computed(() => String(route.params.id))
const request = computed(() => feedStore.byId(id.value))
const isLoading = ref(true)
const isOpen = computed(() => (request.value ? canRespond(request.value) : false))
const threadId = computed(() => feedStore.threadIdOf(id.value))
const isSaving = computed(() => feedStore.isResponding(id.value))

// Always asks the server: the copy on the loaded feed page lacks the chat thread
// and may be out of date (the post may have closed meanwhile). A direct link
// may also point at a request that is not on a loaded page at all.
onMounted(async () => {
  try {
    await feedStore.fetchOne(id.value)
  }
  catch {
    // Shown as "not found" when there is no cached copy either.
  }
  isLoading.value = false
})

useSeoMeta({ title: () => `${request.value?.title ?? t('merchant_feed.details_title')} | Gdzie Kupić` })

const posted = computed(() => (request.value ? formatRelativeTime(request.value.createdAt, locale.value) : ''))
const category = computed(() => (request.value ? `${request.value.category.name} · ${request.value.tag.name}` : ''))
const deadline = computed(() =>
  request.value?.urgentDeadline
    ? new Intl.DateTimeFormat(locale.value, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(request.value.urgentDeadline))
    : null,
)
const radius = computed(() =>
  request.value?.buyerRadiusKm == null
    ? t('merchant_feed.buyer_radius_unlimited')
    : t('merchant_feed.buyer_radius', { km: request.value.buyerRadiusKm }),
)

const respondError = ref('')

async function respond(state: MerchantResponse) {
  respondError.value = ''
  try {
    await feedStore.respond(id.value, state)
  }
  catch (err) {
    respondError.value = parseApiError(err).status === 409
      ? t('merchant_feed.respond_conflict_page')
      : t('merchant_feed.respond_error')
  }
}
</script>

<template>
  <div class="container mx-auto max-w-3xl space-y-6 px-4 py-6 lg:py-8">
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
      <div class="space-y-6">
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
                <dd class="font-medium text-highlighted">
                  {{ request.buyerName }}
                </dd>
              </div>
              <div>
                <dt class="text-muted">
                  {{ $t('request.location') }}
                </dt>
                <dd class="font-medium text-highlighted">
                  {{ $t('merchant_feed.km_from_you', { distance: formatDistance(request.distanceKm, locale) }) }}
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
              :aria-label="$t('merchant_feed.buyer_location_aria', { radius })"
            >
              <span class="absolute size-32 rounded-full border-2 border-primary/20 bg-primary/5" />
              <span class="absolute size-20 rounded-full border-2 border-primary/40 bg-primary/10" />
              <UIcon name="i-heroicons-map-pin" class="relative size-8 text-primary" />
            </div>
            <p class="mt-2 text-sm text-muted">
              {{ radius }}
            </p>
          </UCard>

          <UCard>
            <h2 class="text-base font-semibold text-highlighted">
              {{ $t('merchant_feed.your_response') }}
            </h2>
            <p class="mt-1 mb-3 text-sm text-muted" data-testid="current-response">
              {{ request.myResponse ? $t(`merchant_feed.response.${request.myResponse}`) : $t('merchant_feed.no_response_yet') }}
            </p>
            <MerchantResponseButtons :current="request.myResponse" :disabled="!isOpen || isSaving" @select="respond" />
            <UAlert
              v-if="!isOpen"
              class="mt-3"
              color="neutral"
              variant="subtle"
              icon="i-heroicons-lock-closed"
              data-testid="closed-notice"
              :description="$t('merchant_feed.closed_notice', { status: $t(`request.status.${request.status}`) })"
            />
            <UButton
              v-if="threadId"
              class="mt-3"
              :to="threadPath(threadId)"
              variant="soft"
              icon="i-heroicons-chat-bubble-left-right"
              data-testid="open-thread"
            >
              {{ $t('merchant_feed.open_thread') }}
            </UButton>
            <UAlert
              v-if="respondError"
              class="mt-3"
              color="error"
              variant="subtle"
              icon="i-heroicons-exclamation-circle"
              :description="respondError"
            />
          </UCard>
      </div>
    </template>
  </div>
</template>
