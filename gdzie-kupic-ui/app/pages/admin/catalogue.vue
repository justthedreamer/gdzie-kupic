<script setup lang="ts">
definePageMeta({
  layout: 'admin',
  middleware: ['auth', 'role'],
  roles: ['Admin'],
  shellTitleKey: 'catalogue.title',
})

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

// ─── Mutations ───────────────────────────────────────────────────────────────
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

// ─── Selection ───────────────────────────────────────────────────────────────
const selectedId = ref<string | null>(null)
const selectedCategory = computed(() =>
  categories.value.find(category => category.id === selectedId.value) ?? categories.value[0] ?? null,
)
const categoryOptions = computed(() =>
  categories.value.map(category => ({ value: category.id, label: category.name })),
)

const tab = ref<'tags' | 'details'>('tags')
const tabItems = computed(() => [
  { value: 'tags', label: `${t('catalogue.tags')} (${selectedCategory.value?.tags.length ?? 0})` },
  { value: 'details', label: t('catalogue.category_details') },
])

watch(tab, () => { actionError.value = '' })

function selectCategory(id: string) {
  selectedId.value = id
  tab.value = 'tags'
  actionError.value = ''
  closeEditor()
}

// ─── Create category ─────────────────────────────────────────────────────────
const addCategoryOpen = ref(false)
const newCategoryName = ref('')

function openAddCategory() {
  newCategoryName.value = ''
  actionError.value = ''
  addCategoryOpen.value = true
}

async function addCategory() {
  const name = newCategoryName.value.trim()
  if (!name) return

  let created: CatalogueCategory | undefined
  const ok = await mutate('new-category', async () => {
    created = await catalogueApi.createCategory(name)
  })

  if (ok) {
    addCategoryOpen.value = false
    if (created) selectCategory(created.id)
  }
}

// ─── Edit category ───────────────────────────────────────────────────────────
async function saveCategory(draft: { name: string, isDisabled: boolean }) {
  const category = selectedCategory.value
  if (!category) return

  if (draft.name !== category.name
    && !await mutate(category.id, () => catalogueApi.renameCategory(category.id, draft.name))) return

  if (draft.isDisabled !== category.isDisabled) {
    await mutate(category.id, () => catalogueApi.setCategoryDisabled(category.id, draft.isDisabled))
  }
}

// ─── Tags ────────────────────────────────────────────────────────────────────
// `null` = closed; `{ tagId: null }` = creating a tag; otherwise editing.
const editor = ref<{ tagId: string | null } | null>(null)
const editorTag = computed(() =>
  editor.value?.tagId
    ? selectedCategory.value?.tags.find(tag => tag.id === editor.value?.tagId) ?? null
    : null,
)
const editorOpen = computed(() => !!editor.value && (editor.value.tagId === null || !!editorTag.value))

function openEditor(tagId: string | null) {
  actionError.value = ''
  editor.value = { tagId }
}

function closeEditor() {
  editor.value = null
}

async function saveTag(draft: { name: string, isDisabled: boolean }) {
  const category = selectedCategory.value
  if (!category) return

  const tag = editorTag.value
  let ok = true

  if (!tag) {
    ok = await mutate('new-tag', () => catalogueApi.createTag(category.id, draft.name))
  }
  else {
    if (draft.name !== tag.name) {
      ok = await mutate(tag.id, () => catalogueApi.renameTag(tag.id, draft.name))
    }
    if (ok && draft.isDisabled !== tag.isDisabled) {
      ok = await mutate(tag.id, () => catalogueApi.setTagDisabled(tag.id, draft.isDisabled))
    }
  }

  if (ok) closeEditor()
}

function toggleTag(tag: CatalogueTag) {
  return mutate(tag.id, () => catalogueApi.setTagDisabled(tag.id, !tag.isDisabled))
}
</script>

<template>
  <div class="space-y-6 p-4 lg:p-8">
    <header class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">
          {{ $t('catalogue.title') }}
        </h1>
        <p class="mt-1 text-sm text-muted">
          {{ $t('catalogue.description') }}
        </p>
      </div>
      <UButton icon="i-heroicons-plus" @click="openAddCategory">
        {{ $t('catalogue.add_category') }}
      </UButton>
    </header>

    <UAlert
      v-if="actionError && !editorOpen && !addCategoryOpen && tab === 'tags'"
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
        :description="$t('catalogue.load_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="refresh()">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <p v-else-if="!selectedCategory" class="py-10 text-center text-muted">
      {{ $t('catalogue.empty') }}
    </p>

    <div v-else class="flex items-start gap-4">
      <aside class="hidden w-72 shrink-0 lg:block">
        <CatalogueCategoryList
          :categories="categories"
          :selected-id="selectedCategory.id"
          @select="selectCategory"
        />
      </aside>

      <section class="min-w-0 flex-1 space-y-4">
        <USelect
          :model-value="selectedCategory.id"
          :items="categoryOptions"
          :aria-label="$t('catalogue.category')"
          class="w-full lg:hidden"
          @update:model-value="selectCategory(String($event))"
        />

        <div class="rounded-xl border border-default bg-default">
          <header class="flex flex-wrap items-center justify-between gap-3 p-4 pb-0">
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <h2
                  class="truncate text-xl font-semibold"
                  :class="selectedCategory.isDisabled ? 'text-muted' : 'text-highlighted'"
                >
                  {{ selectedCategory.name }}
                </h2>
                <UBadge :color="selectedCategory.isDisabled ? 'neutral' : 'success'" variant="subtle" size="sm">
                  {{ selectedCategory.isDisabled ? $t('catalogue.category_disabled') : $t('catalogue.category_active') }}
                </UBadge>
              </div>
              <p class="mt-1 text-sm text-muted">
                {{ $t('catalogue.tag_count', selectedCategory.tags.length) }}
              </p>
            </div>
            <UButton
              color="neutral"
              variant="outline"
              size="sm"
              icon="i-heroicons-pencil-square"
              @click="tab = 'details'"
            >
              {{ $t('catalogue.edit_category') }}
            </UButton>
          </header>

          <UTabs
            v-model="tab"
            :items="tabItems"
            :content="false"
            variant="link"
            class="px-4"
          />

          <div class="p-4 pt-2">
            <CatalogueTagTable
              v-if="tab === 'tags'"
              :key="selectedCategory.id"
              :tags="selectedCategory.tags"
              :busy-id="busyId"
              :editing-id="editorOpen ? editor?.tagId : null"
              @add="openEditor(null)"
              @edit="openEditor($event.id)"
              @toggle="toggleTag"
            />

            <CatalogueCategoryDetails
              v-else
              :category="selectedCategory"
              :busy="busyId === selectedCategory.id"
              :error="actionError"
              @save="saveCategory"
            />
          </div>
        </div>
      </section>

      <template v-if="editorOpen && tab === 'tags'">
        <!-- Below 2xl the panel slides over the page; at 2xl it is a third column. -->
        <div class="fixed inset-0 z-30 bg-black/40 2xl:hidden" @click="closeEditor" />
        <aside
          :aria-label="editorTag ? $t('catalogue.edit_tag') : $t('catalogue.new_tag')"
          class="fixed inset-y-0 right-0 z-40 w-full max-w-sm border-l border-default bg-default shadow-xl 2xl:static 2xl:z-auto 2xl:w-80 2xl:max-w-none 2xl:shrink-0 2xl:rounded-xl 2xl:border 2xl:shadow-none"
        >
          <CatalogueTagEditor
            :key="editor?.tagId ?? 'new'"
            :tag="editorTag"
            :category-name="selectedCategory.name"
            :busy="busyId !== null && (busyId === 'new-tag' || busyId === editor?.tagId)"
            :error="actionError"
            @save="saveTag"
            @close="closeEditor"
          />
        </aside>
      </template>
    </div>

    <UModal v-model:open="addCategoryOpen" :title="$t('catalogue.add_category')">
      <template #body>
        <form id="add-category-form" class="space-y-4" @submit.prevent="addCategory">
          <UAlert
            v-if="actionError"
            color="error"
            variant="subtle"
            icon="i-heroicons-exclamation-circle"
            :description="actionError"
          />
          <UFormField :label="$t('catalogue.category_name')">
            <UInput v-model="newCategoryName" :maxlength="NAME_MAX_LENGTH" autofocus class="w-full" />
          </UFormField>
        </form>
      </template>

      <template #footer>
        <div class="flex w-full justify-end gap-2">
          <UButton color="neutral" variant="outline" @click="addCategoryOpen = false">
            {{ $t('common.cancel') }}
          </UButton>
          <UButton
            type="submit"
            form="add-category-form"
            :loading="busyId === 'new-category'"
            :disabled="!newCategoryName.trim()"
          >
            {{ $t('catalogue.add_category') }}
          </UButton>
        </div>
      </template>
    </UModal>
  </div>
</template>
