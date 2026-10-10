<script setup lang="ts">
definePageMeta({
  layout: 'merchant',
  middleware: ['auth', 'role'],
  roles: ['Merchant'],
  shellTitleKey: 'merchant.subscriptions.title',
})

const { t } = useI18n()
useSeoMeta({ title: () => `${t('merchant.subscriptions.title')} | Gdzie Kupić` })

const merchantApi = useMerchantApi()
const catalogueApi = useCatalogueApi()
const merchantStore = useMerchantStore()

const { data: subscriptions, status, error: subscriptionsError, refresh } = useAsyncData(
  'merchant-subscriptions',
  () => merchantApi.listSubscriptions(),
  { server: false, default: () => [] as MerchantSubscription[] },
)
const { data: categories, status: categoriesStatus, error: categoriesError, refresh: refreshCategories } = useAsyncData(
  'merchant-catalogue',
  () => catalogueApi.getCategories(),
  { server: false, default: () => [] as CatalogueCategory[] },
)
const loadError = computed(() => subscriptionsError.value ?? categoriesError.value)

// Only the first load shows the loading state — a refresh after a toggle must
// not unmount the toggles (and drop focus) while it is in flight.
const loaded = ref(false)
watch([status, categoriesStatus], ([s, c]) => {
  const settled = (v: string) => v !== 'idle' && v !== 'pending'
  if (settled(s) && settled(c)) loaded.value = true
}, { immediate: true })
const isLoading = computed(() => !loaded.value)

// The catalogue read endpoint also returns disabled categories/tags, so
// existing subscriptions to them stay visible and can still be switched off.

// ─── Toggle ─────────────────────────────────────────────────────────────────
// A toggle subscribes or unsubscribes immediately. While a change for a
// category is in flight, all of that category's toggles are locked.
const actionError = ref('')
const pending = ref<Record<string, string>>({}) // categoryId -> key of the toggle in flight

function isSubscribed(categoryId: string, tagId: string | null) {
  return !!findSubscription(subscriptions.value, categoryId, tagId)
}

function isPending(categoryId: string) {
  return categoryId in pending.value
}

function isLoadingToggle(categoryId: string, tagId: string | null) {
  return pending.value[categoryId] === subscriptionKey({ categoryId, tagId })
}

async function subscribeQuietly(target: SubscriptionTarget) {
  try {
    await merchantApi.subscribe(target)
  }
  catch (err) {
    // Already subscribed (e.g. in another tab) — the goal is reached.
    if (parseApiError(err).status !== 409) throw err
  }
}

async function toggle(categoryId: string, tagId: string | null) {
  if (isPending(categoryId)) return

  pending.value = { ...pending.value, [categoryId]: subscriptionKey({ categoryId, tagId }) }
  actionError.value = ''

  try {
    const existing = findSubscription(subscriptions.value, categoryId, tagId)
    const wholeCategory = findSubscription(subscriptions.value, categoryId, null)

    if (tagId !== null && wholeCategory) {
      // Switching one tag off under a whole-category subscription: replace the
      // category with subscriptions to all of its other active tags. The new
      // ones are added first so coverage never lapses.
      const category = categories.value.find(c => c.id === categoryId)
      const others = (category?.tags ?? []).filter(tg => tg.id !== tagId && !tg.isDisabled)

      for (const tag of others) {
        await subscribeQuietly({ categoryId, tagId: tag.id })
      }
      await merchantApi.unsubscribe(wholeCategory.id)
      if (existing) await merchantApi.unsubscribe(existing.id)
    }
    else if (existing) {
      await merchantApi.unsubscribe(existing.id)
    }
    else {
      await subscribeQuietly({ categoryId, tagId })

      // A whole-category subscription already covers every tag, so the
      // individual tag subscriptions of that category become redundant.
      if (tagId === null) {
        for (const redundant of tagSubscriptionsOf(subscriptions.value, categoryId)) {
          await merchantApi.unsubscribe(redundant.id)
        }
      }
    }
  }
  catch (err) {
    actionError.value = resolveApiError(err, {
      byStatus: { 404: t('merchant.subscriptions.errors.not_found') },
      fallback: t('merchant.subscriptions.errors.generic'),
      unavailable: t('merchant.subscriptions.errors.unavailable'),
    })
  }
  finally {
    await refresh()
    const { [categoryId]: _done, ...rest } = pending.value
    pending.value = rest
  }
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-2xl space-y-8">
    <header>
      <h1 class="text-2xl font-bold">
        {{ merchantStore.profile?.name ?? $t('merchant.subscriptions.title') }}
      </h1>
      <p v-if="merchantStore.profile" class="text-sm text-muted mt-1">
        {{ $t('merchant.subscriptions.branch') }}: {{ merchantStore.profile.branch.displayName }}
        <template v-if="merchantStore.profile.branch.addressDisplayName">
          — {{ merchantStore.profile.branch.addressDisplayName }}
        </template>
      </p>
    </header>

    <section class="space-y-3">
      <div>
        <h2 class="text-lg font-semibold">
          {{ $t('merchant.subscriptions.section_title') }}
        </h2>
        <p class="text-sm text-muted">
          {{ $t('merchant.subscriptions.description') }}
        </p>
      </div>

      <UAlert
        v-if="actionError"
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="actionError"
      />

      <p v-if="isLoading" class="text-sm text-muted">
        {{ $t('common.loading') }}
      </p>

      <div v-else-if="loadError" class="space-y-3">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-heroicons-exclamation-circle"
          :description="$t('merchant.subscriptions.load_error')"
        />
        <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh(); refreshCategories()">
          {{ $t('common.retry') }}
        </UButton>
      </div>

      <p v-else-if="!categories.length" class="text-center py-6 text-muted">
        {{ $t('merchant.subscriptions.no_categories') }}
      </p>

      <div v-else class="space-y-3">
        <fieldset
          v-for="category in categories"
          :key="category.id"
          class="rounded-lg border border-default bg-default p-4 space-y-3"
        >
          <legend class="sr-only">
            {{ category.name }}
          </legend>

          <div class="flex items-center justify-between gap-4">
            <div class="flex min-w-0 items-center gap-2">
              <span class="font-semibold truncate">{{ category.name }}</span>
              <UBadge v-if="category.isDisabled" color="neutral" variant="subtle" size="sm">
                {{ $t('catalogue.category_disabled') }}
              </UBadge>
            </div>
            <div class="flex shrink-0 items-center gap-3">
              <span class="text-sm text-muted">{{ $t('merchant.subscriptions.whole_category') }}</span>
              <USwitch
                color="success"
                :model-value="isSubscribed(category.id, null)"
                :loading="isLoadingToggle(category.id, null)"
                :disabled="isPending(category.id) || (category.isDisabled && !isSubscribed(category.id, null))"
                :aria-label="`${$t('merchant.subscriptions.whole_category')}: ${category.name}`"
                @update:model-value="toggle(category.id, null)"
              />
            </div>
          </div>

          <ul v-if="category.tags.length" class="divide-y divide-default border-t border-default">
            <li
              v-for="tag in category.tags"
              :key="tag.id"
              class="flex items-center justify-between gap-4 py-3"
            >
              <div class="flex min-w-0 items-center gap-2">
                <span class="truncate">{{ tag.name }}</span>
                <UBadge v-if="tag.isDisabled" color="neutral" variant="subtle" size="sm">
                  {{ $t('catalogue.tag_disabled') }}
                </UBadge>
              </div>
              <USwitch
                color="success"
                :model-value="isSubscribed(category.id, null) || isSubscribed(category.id, tag.id)"
                :loading="isLoadingToggle(category.id, tag.id)"
                :disabled="isPending(category.id) || ((category.isDisabled || tag.isDisabled) && !isSubscribed(category.id, tag.id))"
                :aria-label="tag.name"
                @update:model-value="toggle(category.id, tag.id)"
              />
            </li>
          </ul>
        </fieldset>
      </div>
    </section>
  </div>
</template>
