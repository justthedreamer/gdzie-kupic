<script setup lang="ts">
definePageMeta({ middleware: ['auth', 'role'], roles: ['Admin'] })

const { t } = useI18n()
useSeoMeta({ title: () => `${t('catalogue.title')} | Gdzie Kupić` })

const catalogueApi = useCatalogueApi()

const { data: categories, status, error: loadError, refresh } = useAsyncData(
  'catalogue-categories',
  () => catalogueApi.getCategories(),
  { server: false, default: () => [] as CatalogueCategory[] },
)
const isLoading = computed(() =>
  (status.value === 'idle' || status.value === 'pending') && !categories.value.length,
)

const actionError = ref('')
const busyId = ref<string | null>(null)

function describeError(err: unknown): string {
  return resolveApiError(err, {
    byStatus: {
      404: t('catalogue.errors.not_found'),
      409: t('catalogue.errors.duplicate'),
    },
    fallback: t('catalogue.errors.generic'),
    unavailable: t('catalogue.errors.unavailable'),
  })
}

// Runs a mutation, refreshes the list on success and reports failures
// without leaving the page. Resolves to whether the mutation succeeded.
async function mutate(id: string, action: () => Promise<unknown>): Promise<boolean> {
  busyId.value = id
  actionError.value = ''

  try {
    await action()
    await refresh()
    return true
  }
  catch (err) {
    actionError.value = describeError(err)
    // A 404 means the list is stale — reload so it matches the server again.
    if (parseApiError(err).status === 404) await refresh()
    return false
  }
  finally {
    busyId.value = null
  }
}

// ─── Create category ────────────────────────────────────────────────────────
const newCategoryName = ref('')

async function addCategory() {
  const name = newCategoryName.value.trim()
  if (!name) return

  if (await mutate('new-category', () => catalogueApi.createCategory(name))) {
    newCategoryName.value = ''
  }
}

// ─── Create tag (draft per category) ────────────────────────────────────────
const newTagNames = reactive<Record<string, string>>({})

async function addTag(categoryId: string) {
  const name = (newTagNames[categoryId] ?? '').trim()
  if (!name) return

  if (await mutate(`new-tag-${categoryId}`, () => catalogueApi.createTag(categoryId, name))) {
    newTagNames[categoryId] = ''
  }
}
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-3xl space-y-8">
    <header>
      <h1 class="text-2xl font-bold">
        {{ $t('catalogue.title') }}
      </h1>
      <p class="text-sm text-muted mt-1">
        {{ $t('catalogue.description') }}
      </p>
    </header>

    <UAlert
      v-if="actionError"
      color="error"
      variant="subtle"
      icon="i-heroicons-exclamation-circle"
      :description="actionError"
    />

    <form class="flex items-end gap-2" @submit.prevent="addCategory">
      <UFormField :label="$t('catalogue.category_name')" class="flex-1">
        <UInput v-model="newCategoryName" maxlength="100" class="w-full" />
      </UFormField>
      <UButton
        type="submit"
        icon="i-heroicons-plus"
        :loading="busyId === 'new-category'"
        :disabled="!newCategoryName.trim()"
      >
        {{ $t('catalogue.add_category') }}
      </UButton>
    </form>

    <p v-if="isLoading" class="text-sm text-muted">
      {{ $t('common.loading') }}
    </p>

    <div v-else-if="loadError" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('catalogue.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <p v-else-if="!categories.length" class="text-center py-10 text-muted">
      {{ $t('catalogue.empty') }}
    </p>

    <div v-else class="space-y-4">
      <UCard v-for="category in categories" :key="category.id" :data-testid="`category-${category.id}`">
        <template #header>
          <CatalogueItemRow
            heading
            :name="category.name"
            :is-disabled="category.isDisabled"
            :busy="busyId === category.id"
            :rename-fn="name => mutate(category.id, () => catalogueApi.renameCategory(category.id, name))"
            @toggle="mutate(category.id, () => catalogueApi.setCategoryDisabled(category.id, !category.isDisabled))"
          />
        </template>

        <ul v-if="category.tags.length" class="divide-y divide-default">
          <li v-for="tag in category.tags" :key="tag.id" class="py-2">
            <CatalogueItemRow
              :name="tag.name"
              :is-disabled="tag.isDisabled"
              :busy="busyId === tag.id"
              :rename-fn="name => mutate(tag.id, () => catalogueApi.renameTag(tag.id, name))"
              @toggle="mutate(tag.id, () => catalogueApi.setTagDisabled(tag.id, !tag.isDisabled))"
            />
          </li>
        </ul>
        <p v-else class="text-sm text-muted">
          {{ $t('catalogue.no_tags') }}
        </p>

        <template #footer>
          <form class="flex items-center gap-2" @submit.prevent="addTag(category.id)">
            <UInput
              v-model="newTagNames[category.id]"
              :placeholder="$t('catalogue.tag_name')"
              :aria-label="`${$t('catalogue.tag_name')}: ${category.name}`"
              maxlength="100"
              class="flex-1"
            />
            <UButton
              type="submit"
              size="sm"
              variant="subtle"
              icon="i-heroicons-plus"
              :aria-label="`${$t('catalogue.add_tag')}: ${category.name}`"
              :loading="busyId === `new-tag-${category.id}`"
              :disabled="!(newTagNames[category.id] ?? '').trim()"
            >
              {{ $t('catalogue.add_tag') }}
            </UButton>
          </form>
        </template>
      </UCard>
    </div>
  </div>
</template>
