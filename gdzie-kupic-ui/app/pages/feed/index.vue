<script setup lang="ts">
import { DISTANCE_OPTIONS_KM, type FeedSort, type FeedTab, type MerchantFeedRequest, type MerchantResponse } from '~/utils/merchantFeed'

definePageMeta({
  layout: 'merchant',
  middleware: ['auth', 'role'],
  roles: ['Merchant'],
  shellTitleKey: 'merchant_feed.title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('merchant_feed.title')} | Gdzie Kupić` })

const feedStore = useMerchantFeedStore()
onMounted(() => feedStore.load())

const requests = computed(() => feedStore.requests)
const isLoading = computed(() => feedStore.status === 'idle' || feedStore.status === 'pending')
const hasError = computed(() => feedStore.status === 'error')
// Nothing at all for this merchant (not just an empty tab): the summary counts every tab.
const hasNothing = computed(() => feedStore.summary !== null && feedStore.summary.newCount + feedStore.summary.respondedCount === 0)

// ─── Filters ────────────────────────────────────────────────────────────────
// The server filters and sorts: every change reloads the feed from its first page.
const ALL = '__all__'
const filtersOpen = ref(false)

const tab = computed<FeedTab>({
  get: () => feedStore.filters.tab,
  set: (value) => { void feedStore.setFilters({ tab: value }) },
})
const category = computed<string>({
  get: () => feedStore.filters.categoryId ?? ALL,
  set: (value) => { void feedStore.setFilters({ categoryId: value === ALL ? null : value }) },
})
const distance = computed<number>({
  get: () => feedStore.filters.maxDistanceKm ?? 0,
  set: (value) => { void feedStore.setFilters({ maxDistanceKm: value === 0 ? null : value }) },
})
const sort = computed<FeedSort>({
  get: () => feedStore.filters.sort,
  set: (value) => { void feedStore.setFilters({ sort: value }) },
})

const tabs = computed(() => {
  const summary = feedStore.summary
  return [
    { label: t('merchant_feed.tabs.new'), value: 'new', badge: summary?.newCount },
    { label: t('merchant_feed.tabs.responded'), value: 'responded', badge: summary?.respondedCount },
    { label: t('merchant_feed.tabs.all'), value: 'all', badge: summary ? summary.newCount + summary.respondedCount : undefined },
  ]
})

const categoryItems = computed(() => [
  { label: t('merchant_feed.filters.all_categories'), value: ALL },
  ...feedStore.categories.map(({ id, name }) => ({ label: name, value: id })),
])
const distanceItems = computed(() => [
  { label: t('merchant_feed.filters.any_distance'), value: 0 },
  ...DISTANCE_OPTIONS_KM.map(km => ({ label: `${km} km`, value: km })),
])
const sortItems = computed(() => [
  { label: t('merchant_feed.filters.newest'), value: 'newest' },
  { label: t('merchant_feed.filters.nearest'), value: 'nearest' },
])

// ─── Responding ─────────────────────────────────────────────────────────────
const respondError = ref('')

async function respond(request: MerchantFeedRequest, state: MerchantResponse) {
  respondError.value = ''
  try {
    await feedStore.respond(request.id, state)
  }
  catch (err) {
    if (parseApiError(err).status === 409) {
      // The post closed or expired in the meantime: show what is still open.
      respondError.value = t('merchant_feed.respond_conflict')
      await feedStore.load(true)
    }
    else {
      respondError.value = t('merchant_feed.respond_error')
    }
  }
}

// ─── Infinite scroll ────────────────────────────────────────────────────────
const sentinel = ref<HTMLElement | null>(null)
useInfiniteScroll(sentinel, () => feedStore.loadMore(), { refresh: () => requests.value.length })
</script>

<template>
  <div class="container mx-auto max-w-4xl space-y-4 px-4 py-6 lg:py-8">
    <div class="flex items-start justify-between gap-4">
      <div>
        <h1 class="hidden text-2xl font-bold text-highlighted lg:block">
          {{ $t('merchant_feed.title') }}
        </h1>
        <p class="text-sm text-muted lg:mt-1">
          {{ $t('merchant_feed.subtitle') }}
        </p>
      </div>
      <div class="flex shrink-0 gap-2">
        <UButton
          class="lg:hidden"
          color="neutral"
          variant="outline"
          icon="i-heroicons-funnel"
          :aria-label="$t('merchant_feed.filters.toggle')"
          :aria-expanded="filtersOpen"
          @click="filtersOpen = !filtersOpen"
        />
        <UButton
          color="neutral"
          variant="outline"
          icon="i-heroicons-arrow-path"
          :loading="isLoading"
          :aria-label="$t('merchant_feed.refresh')"
          @click="feedStore.load(true)"
        />
      </div>
    </div>

    <PushBanner />

    <UCard v-if="hasNothing && !hasError">
      <div class="space-y-3 py-8 text-center">
        <UIcon name="i-heroicons-inbox" class="size-10 text-muted" />
        <p class="font-medium text-highlighted">
          {{ $t('merchant_feed.empty_title') }}
        </p>
        <p class="text-sm text-muted">
          {{ $t('merchant_feed.empty_text') }}
        </p>
      </div>
    </UCard>

    <template v-else>
      <UTabs
        v-model="tab"
        :items="tabs"
        :content="false"
        :aria-label="$t('merchant_feed.tabs.label')"
      />

      <div
        :class="filtersOpen ? 'grid' : 'hidden lg:grid'"
        class="grid-cols-1 gap-3 sm:grid-cols-3"
      >
        <USelect v-model="category" :items="categoryItems" :aria-label="$t('request.category')" />
        <USelect v-model="distance" :items="distanceItems" :aria-label="$t('merchant_feed.filters.distance')" />
        <USelect v-model="sort" :items="sortItems" :aria-label="$t('merchant_feed.filters.sort')" />
      </div>

      <UAlert
        v-if="respondError"
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="respondError"
      />

      <p v-if="isLoading && !requests.length" class="text-sm text-muted">
        {{ $t('common.loading') }}
      </p>

      <div v-else-if="hasError" class="space-y-3">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('merchant_feed.load_error')"
        />
        <UButton variant="outline" icon="i-heroicons-arrow-path" @click="feedStore.load(true)">
          {{ $t('common.retry') }}
        </UButton>
      </div>

      <template v-else-if="requests.length">
        <ul class="space-y-4">
          <li v-for="request in requests" :key="request.id">
            <MerchantFeedCard :request="request" :busy="feedStore.isResponding(request.id)" @respond="respond(request, $event)" />
          </li>
        </ul>

        <!-- The sentinel loads the next page when it scrolls into view; the button is the manual way (and the retry). -->
        <div
          v-if="feedStore.nextCursor"
          ref="sentinel"
          class="flex flex-col items-center gap-2 py-2"
          data-testid="feed-more"
        >
          <p v-if="feedStore.loadMoreFailed" class="text-sm text-error">
            {{ $t('merchant_feed.load_more_error') }}
          </p>
          <UButton
            color="neutral"
            variant="outline"
            :loading="feedStore.loadingMore"
            @click="feedStore.loadMore()"
          >
            {{ feedStore.loadMoreFailed ? $t('common.retry') : $t('merchant_feed.load_more') }}
          </UButton>
        </div>
      </template>

      <UCard v-else>
        <p class="py-6 text-center text-sm text-muted">
          {{ $t(`merchant_feed.empty_tab.${tab}`) }}
        </p>
      </UCard>
    </template>
  </div>
</template>
