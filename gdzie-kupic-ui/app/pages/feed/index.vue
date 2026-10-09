<script setup lang="ts">
import {
  DISTANCE_OPTIONS_KM,
  feedCategories,
  filterFeed,
  unansweredCount,
  type FeedSort,
  type FeedTab,
} from '~/utils/merchantFeed'

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

const isLoading = computed(() => feedStore.status === 'idle' || feedStore.status === 'pending')
const hasError = computed(() => feedStore.status === 'error')

// ─── Filters ────────────────────────────────────────────────────────────────
const ALL = '__all__'
const tab = ref<FeedTab>('new')
const category = ref<string>(ALL)
const distance = ref<number>(0)
const sort = ref<FeedSort>('newest')
const filtersOpen = ref(false)

const requests = computed(() => feedStore.requests)

const tabs = computed(() => [
  { label: t('merchant_feed.tabs.new'), value: 'new', badge: unansweredCount(requests.value) },
  { label: t('merchant_feed.tabs.responded'), value: 'responded' },
  { label: t('merchant_feed.tabs.all'), value: 'all' },
])

const categoryItems = computed(() => [
  { label: t('merchant_feed.filters.all_categories'), value: ALL },
  ...feedCategories(requests.value).map(name => ({ label: name, value: name })),
])
const distanceItems = computed(() => [
  { label: t('merchant_feed.filters.any_distance'), value: 0 },
  ...DISTANCE_OPTIONS_KM.map(km => ({ label: `${km} km`, value: km })),
])
const sortItems = computed(() => [
  { label: t('merchant_feed.filters.newest'), value: 'newest' },
  { label: t('merchant_feed.filters.nearest'), value: 'nearest' },
])

const visible = computed(() =>
  filterFeed(requests.value, {
    tab: tab.value,
    category: category.value === ALL ? null : category.value,
    maxDistanceKm: distance.value === 0 ? null : distance.value,
    sort: sort.value,
  }),
)
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

    <UCard v-else-if="!requests.length">
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

      <ul v-if="visible.length" class="space-y-4">
        <li v-for="request in visible" :key="request.id">
          <MerchantFeedCard :request="request" @respond="feedStore.respond(request.id, $event)" />
        </li>
      </ul>

      <UCard v-else>
        <p class="py-6 text-center text-sm text-muted">
          {{ $t(`merchant_feed.empty_tab.${tab}`) }}
        </p>
      </UCard>
    </template>
  </div>
</template>
