<script setup lang="ts">
// "Category Details" tab: rename and enable/disable the category. The parent
// performs the mutations and passes failures back through `error`.
const props = defineProps<{
  category: CatalogueCategory
  busy?: boolean
  error?: string
}>()

const emit = defineEmits<{
  save: [draft: { name: string, isDisabled: boolean }]
}>()

const name = ref('')
const isActive = ref(true)

function reset() {
  name.value = props.category.name
  isActive.value = !props.category.isDisabled
}

reset()
watch(() => [props.category.id, props.category.name, props.category.isDisabled], reset)

const trimmed = computed(() => name.value.trim())
const canSave = computed(() =>
  !!trimmed.value && (trimmed.value !== props.category.name || isActive.value === props.category.isDisabled),
)

function submit() {
  if (!canSave.value) return
  emit('save', { name: trimmed.value, isDisabled: !isActive.value })
}
</script>

<template>
  <form class="max-w-lg space-y-5" @submit.prevent="submit">
    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      icon="i-heroicons-exclamation-circle"
      :description="error"
    />

    <UFormField :label="$t('catalogue.category_name')">
      <UInput v-model="name" :maxlength="NAME_MAX_LENGTH" class="w-full" />
      <template #hint>
        <span class="text-xs">{{ $t('catalogue.characters', { n: name.length, max: NAME_MAX_LENGTH }) }}</span>
      </template>
    </UFormField>

    <UFormField :label="$t('catalogue.status')" :help="$t('catalogue.category_status_hint')">
      <USwitch
        v-model="isActive"
        :label="isActive ? $t('catalogue.category_active') : $t('catalogue.category_disabled')"
      />
    </UFormField>

    <dl class="grid grid-cols-2 gap-3 rounded-lg bg-elevated p-4 text-sm">
      <div>
        <dt class="text-muted">
          {{ $t('catalogue.tags_total') }}
        </dt>
        <dd class="text-lg font-semibold text-highlighted">
          {{ category.tags.length }}
        </dd>
      </div>
      <div>
        <dt class="text-muted">
          {{ $t('catalogue.tags_active') }}
        </dt>
        <dd class="text-lg font-semibold text-highlighted">
          {{ activeCount(category.tags) }}
        </dd>
      </div>
    </dl>

    <UButton type="submit" :loading="busy" :disabled="!canSave">
      {{ $t('catalogue.save_changes') }}
    </UButton>
  </form>
</template>
