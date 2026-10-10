<script setup lang="ts">
// Checkbox tree of the catalogue: pick a whole category or individual tags.
// Disabled categories/tags are shown but cannot be selected. Selecting a whole
// category covers all of its tags, so those tag checkboxes are locked.
const props = defineProps<{
  categories: CatalogueCategory[]
  modelValue: SubscriptionTarget[]
}>()

const emit = defineEmits<{
  'update:modelValue': [value: SubscriptionTarget[]]
}>()

function onToggleCategory(categoryId: string) {
  emit('update:modelValue', toggleCategory(props.modelValue, categoryId))
}

function onToggleTag(categoryId: string, tagId: string) {
  emit('update:modelValue', toggleTag(props.modelValue, categoryId, tagId))
}
</script>

<template>
  <div class="space-y-4">
    <fieldset
      v-for="category in categories"
      :key="category.id"
      class="rounded-lg border border-default bg-default p-4 space-y-3"
    >
      <legend class="sr-only">
        {{ category.name }}
      </legend>

      <div class="flex items-center gap-2">
        <UCheckbox
          :model-value="isCategorySelected(modelValue, category.id)"
          :disabled="category.isDisabled"
          :label="category.name"
          :description="$t('merchant.onboarding.whole_category')"
          :ui="{ label: 'font-semibold' }"
          @update:model-value="onToggleCategory(category.id)"
        />
        <UBadge v-if="category.isDisabled" color="neutral" variant="subtle" size="sm">
          {{ $t('catalogue.category_disabled') }}
        </UBadge>
      </div>

      <div v-if="category.tags.length" class="grid sm:grid-cols-2 gap-2 pl-6">
        <div v-for="tag in category.tags" :key="tag.id" class="flex items-center gap-2">
          <UCheckbox
            :model-value="isCategorySelected(modelValue, category.id) || isTagSelected(modelValue, category.id, tag.id)"
            :disabled="category.isDisabled || tag.isDisabled || isCategorySelected(modelValue, category.id)"
            :label="tag.name"
            @update:model-value="onToggleTag(category.id, tag.id)"
          />
          <UBadge v-if="tag.isDisabled" color="neutral" variant="subtle" size="sm">
            {{ $t('catalogue.tag_disabled') }}
          </UBadge>
        </div>
      </div>
    </fieldset>
  </div>
</template>
