<script setup lang="ts">
// "Tags" tab of a category: search, status filter, table with row actions and
// client-side pagination. Mutations are left to the parent.
const props = defineProps<{
  tags: CatalogueTag[]
  busyId?: string | null
  editingId?: string | null
}>()

const emit = defineEmits<{
  add: []
  edit: [tag: CatalogueTag]
  toggle: [tag: CatalogueTag]
}>()

const { t } = useI18n()

const query = ref('')
const status = ref<TagStatusFilter>('all')
const pageSize = ref(PAGE_SIZES[0]!)
const page = ref(1)

const filtered = computed(() => filterTags(props.tags, query.value, status.value))
const slice = computed(() => paginate(filtered.value, page.value, pageSize.value))

// Changing the filter or the page size starts from the first page again.
watch([query, status, pageSize], () => { page.value = 1 })

const statusItems = computed(() =>
  TAG_STATUS_FILTERS.map(value => ({ value, label: t(`catalogue.filter_${value}`) })),
)
const pageSizeItems = PAGE_SIZES.map(size => ({ value: String(size), label: String(size) }))
const pageSizeModel = computed({
  get: () => String(pageSize.value),
  set: (value: string) => { pageSize.value = Number(value) },
})
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center gap-2">
      <UInput
        v-model="query"
        type="search"
        icon="i-heroicons-magnifying-glass"
        :placeholder="$t('catalogue.search_tags')"
        :aria-label="$t('catalogue.search_tags')"
        class="min-w-40 flex-1"
      />
      <USelect
        v-model="status"
        :items="statusItems"
        :aria-label="$t('catalogue.filter_status')"
        class="w-44"
      />
      <UButton variant="outline" icon="i-heroicons-plus" @click="emit('add')">
        {{ $t('catalogue.add_tag') }}
      </UButton>
    </div>

    <div v-if="slice.total" class="overflow-x-auto rounded-lg border border-default">
      <table class="w-full text-sm">
        <thead class="bg-elevated text-left text-xs font-semibold text-muted">
          <tr>
            <th scope="col" class="px-4 py-3">
              {{ $t('catalogue.tag_name') }}
            </th>
            <th scope="col" class="px-4 py-3">
              {{ $t('catalogue.status') }}
            </th>
            <th scope="col" class="px-4 py-3 text-right">
              {{ $t('catalogue.actions') }}
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-default">
          <tr
            v-for="tag in slice.items"
            :key="tag.id"
            :class="tag.id === editingId ? 'bg-primary/5' : ''"
            :data-testid="`tag-${tag.id}`"
          >
            <td class="px-4 py-3 font-medium" :class="tag.isDisabled ? 'text-muted' : 'text-highlighted'">
              {{ tag.name }}
            </td>
            <td class="px-4 py-3">
              <UBadge :color="tag.isDisabled ? 'error' : 'success'" variant="subtle" size="sm">
                {{ tag.isDisabled ? $t('catalogue.tag_disabled') : $t('catalogue.tag_active') }}
              </UBadge>
            </td>
            <td class="px-4 py-3">
              <div class="flex justify-end gap-1">
                <UButton
                  size="xs"
                  color="neutral"
                  variant="outline"
                  icon="i-heroicons-pencil-square"
                  :aria-label="`${$t('catalogue.edit_tag')}: ${tag.name}`"
                  @click="emit('edit', tag)"
                />
                <UButton
                  size="xs"
                  color="neutral"
                  variant="outline"
                  :icon="tag.isDisabled ? 'i-heroicons-eye' : 'i-heroicons-eye-slash'"
                  :aria-label="`${tag.isDisabled ? $t('catalogue.enable') : $t('catalogue.disable')}: ${tag.name}`"
                  :loading="busyId === tag.id"
                  @click="emit('toggle', tag)"
                />
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <p v-else class="rounded-lg border border-dashed border-default p-6 text-center text-sm text-muted">
      {{ tags.length ? $t('catalogue.no_tags_found') : $t('catalogue.no_tags') }}
    </p>

    <div v-if="slice.total" class="flex flex-wrap items-center justify-between gap-3 text-sm text-muted">
      <p data-testid="tag-range">
        {{ $t('catalogue.showing', { from: slice.from, to: slice.to, total: slice.total }) }}
      </p>

      <UPagination
        v-if="slice.pageCount > 1"
        v-model:page="page"
        :total="slice.total"
        :items-per-page="pageSize"
        :show-controls="true"
        size="sm"
      />

      <label class="flex items-center gap-2">
        {{ $t('catalogue.rows_per_page') }}
        <USelect v-model="pageSizeModel" :items="pageSizeItems" size="sm" class="w-20" />
      </label>
    </div>
  </div>
</template>
