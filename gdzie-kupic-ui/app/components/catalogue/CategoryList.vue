<script setup lang="ts">
// Master list of the Admin catalogue: searchable categories with their tag
// count; the selected one is shown in the detail next to it.
const props = defineProps<{
  categories: CatalogueCategory[]
  selectedId: string | null
}>()

const emit = defineEmits<{
  select: [id: string]
}>()

const query = ref('')
const visible = computed(() => filterByName(props.categories, query.value))
</script>

<template>
  <section class="rounded-xl border border-default bg-default">
    <header class="flex items-center justify-between gap-2 p-4 pb-3">
      <h2 class="text-base font-semibold text-highlighted">
        {{ $t('catalogue.categories') }}
      </h2>
      <UBadge color="neutral" variant="subtle" data-testid="category-count">
        {{ categories.length }}
      </UBadge>
    </header>

    <div class="px-4 pb-3">
      <UInput
        v-model="query"
        type="search"
        icon="i-heroicons-magnifying-glass"
        :placeholder="$t('catalogue.search_categories')"
        :aria-label="$t('catalogue.search_categories')"
        class="w-full"
      />
    </div>

    <ul v-if="visible.length" class="divide-y divide-default border-t border-default">
      <li v-for="category in visible" :key="category.id">
        <button
          type="button"
          class="flex w-full items-center gap-3 border-l-4 px-4 py-3 text-left transition-colors hover:bg-elevated"
          :class="category.id === selectedId ? 'border-primary bg-primary/10' : 'border-transparent'"
          :aria-current="category.id === selectedId ? 'true' : undefined"
          :data-testid="`category-${category.id}`"
          @click="emit('select', category.id)"
        >
          <span class="min-w-0 flex-1">
            <span
              class="block truncate text-sm font-medium"
              :class="category.isDisabled ? 'text-muted line-through' : 'text-highlighted'"
            >
              {{ category.name }}
            </span>
            <span class="block text-xs text-muted">
              {{ $t('catalogue.tag_count', category.tags.length) }}
            </span>
          </span>
          <UBadge v-if="category.isDisabled" color="neutral" variant="subtle" size="sm">
            {{ $t('catalogue.category_disabled') }}
          </UBadge>
          <UIcon name="i-heroicons-chevron-right" class="size-4 shrink-0 text-muted" />
        </button>
      </li>
    </ul>
    <p v-else class="border-t border-default p-4 text-sm text-muted">
      {{ $t('catalogue.no_categories_found') }}
    </p>
  </section>
</template>
