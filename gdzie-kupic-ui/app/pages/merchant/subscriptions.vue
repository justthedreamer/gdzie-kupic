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

const { data: subscriptions, status, error: loadError, refresh } = useAsyncData(
  'merchant-subscriptions',
  () => merchantApi.listSubscriptions(),
  { server: false, default: () => [] as MerchantSubscription[] },
)
const { data: categories, refresh: refreshCategories } = useAsyncData(
  'merchant-catalogue',
  () => catalogueApi.getCategories(),
  { server: false, default: () => [] as CatalogueCategory[] },
)
const isLoading = computed(() =>
  (status.value === 'idle' || status.value === 'pending') && !subscriptions.value.length,
)

// Existing subscriptions may point at categories/tags that were disabled
// later; the catalogue read endpoint includes those, so names always resolve.
const rows = computed(() =>
  subscriptions.value.map((subscription) => {
    const category = categories.value.find(c => c.id === subscription.categoryId)
    const tag = category?.tags.find(tg => tg.id === subscription.tagId)

    return {
      id: subscription.id,
      categoryName: category?.name ?? '—',
      tagName: subscription.tagId ? (tag?.name ?? '—') : null,
      isDisabled: !!category?.isDisabled || !!tag?.isDisabled,
    }
  }),
)

// ─── Remove ─────────────────────────────────────────────────────────────────
const actionError = ref('')
const removingId = ref<string | null>(null)

async function remove(id: string) {
  removingId.value = id
  actionError.value = ''

  try {
    await merchantApi.unsubscribe(id)
  }
  catch (err) {
    actionError.value = resolveApiError(err, {
      byStatus: { 404: t('merchant.subscriptions.errors.not_found') },
      fallback: t('merchant.subscriptions.errors.generic'),
      unavailable: t('merchant.subscriptions.errors.unavailable'),
    })
  }
  finally {
    removingId.value = null
  }

  await refresh()
}

// ─── Add ────────────────────────────────────────────────────────────────────
const selection = ref<SubscriptionTarget[]>([])
const adding = ref(false)

async function addSelected() {
  if (!selection.value.length) return

  adding.value = true
  actionError.value = ''

  try {
    selection.value = await merchantApi.subscribeMany(selection.value)
    if (selection.value.length) {
      actionError.value = t('merchant.onboarding.errors.subscriptions')
    }
    await refresh()
  }
  finally {
    adding.value = false
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

      <p v-else-if="!rows.length" class="text-center py-6 text-muted">
        {{ $t('merchant.subscriptions.empty') }}
      </p>

      <ul v-else class="space-y-2">
        <li
          v-for="row in rows"
          :key="row.id"
          class="flex items-center justify-between gap-4 rounded-lg border border-default bg-default p-4"
        >
          <div class="flex min-w-0 items-center gap-2">
            <span class="font-medium truncate">
              {{ row.categoryName }}<template v-if="row.tagName"> › {{ row.tagName }}</template>
            </span>
            <UBadge v-if="!row.tagName" variant="subtle" size="sm">
              {{ $t('merchant.subscriptions.whole_category') }}
            </UBadge>
            <UBadge v-if="row.isDisabled" color="neutral" variant="subtle" size="sm">
              {{ $t('catalogue.disabled') }}
            </UBadge>
          </div>
          <UButton
            color="error"
            variant="ghost"
            icon="i-heroicons-trash"
            :loading="removingId === row.id"
            :aria-label="`${$t('merchant.subscriptions.remove')}: ${row.categoryName}${row.tagName ? ` › ${row.tagName}` : ''}`"
            @click="remove(row.id)"
          />
        </li>
      </ul>
    </section>

    <section v-if="categories.length" class="space-y-3">
      <h2 class="text-lg font-semibold">
        {{ $t('merchant.subscriptions.add_title') }}
      </h2>
      <SubscriptionPicker v-model="selection" :categories="categories" />
      <UButton :loading="adding" :disabled="!selection.length" @click="addSelected">
        {{ $t('merchant.subscriptions.add_selected') }}
      </UButton>
    </section>
  </div>
</template>
