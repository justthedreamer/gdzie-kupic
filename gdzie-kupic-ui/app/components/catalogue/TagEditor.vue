<script setup lang="ts">
// Right-hand panel of the Admin catalogue: edits an existing tag (`tag`) or
// creates a new one (`tag` is null). Mutations are left to the parent, which
// closes the panel on success and passes failures back through `error`.
const props = defineProps<{
  tag: CatalogueTag | null
  categoryName: string
  busy?: boolean
  error?: string
}>()

const emit = defineEmits<{
  save: [draft: { name: string, isDisabled: boolean }]
  close: []
}>()

const name = ref('')
const isActive = ref(true)

function reset() {
  name.value = props.tag?.name ?? ''
  isActive.value = !props.tag?.isDisabled
}

reset()
// Pick up changes made elsewhere (e.g. the row toggle) for the tag being edited.
watch(() => [props.tag?.id, props.tag?.name, props.tag?.isDisabled], reset)

const trimmed = computed(() => name.value.trim())
const canSave = computed(() => {
  if (!trimmed.value) return false
  if (!props.tag) return true
  return trimmed.value !== props.tag.name || isActive.value === props.tag.isDisabled
})

function submit() {
  if (!canSave.value) return
  emit('save', { name: trimmed.value, isDisabled: !isActive.value })
}
</script>

<template>
  <form
    class="flex h-full flex-col gap-5 overflow-y-auto p-5"
    @submit.prevent="submit"
    @keydown.esc="emit('close')"
  >
    <header class="flex items-center justify-between gap-2">
      <h2 class="text-lg font-semibold text-highlighted">
        {{ tag ? $t('catalogue.edit_tag') : $t('catalogue.new_tag') }}
      </h2>
      <UButton
        type="button"
        color="neutral"
        variant="ghost"
        icon="i-heroicons-x-mark"
        :aria-label="$t('common.close')"
        @click="emit('close')"
      />
    </header>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      icon="i-heroicons-exclamation-circle"
      :description="error"
    />

    <div class="space-y-2">
      <p class="text-sm font-medium text-highlighted">
        {{ $t('catalogue.preview') }}
      </p>
      <UBadge color="primary" variant="subtle" size="lg" class="max-w-full">
        <span class="truncate">{{ trimmed || $t('catalogue.tag_name') }}</span>
      </UBadge>
    </div>

    <UFormField :label="$t('catalogue.tag_name')">
      <UInput
        v-model="name"
        :maxlength="NAME_MAX_LENGTH"
        autofocus
        class="w-full"
      />
      <template #hint>
        <span class="text-xs">{{ $t('catalogue.characters', { n: name.length, max: NAME_MAX_LENGTH }) }}</span>
      </template>
    </UFormField>

    <UFormField
      :label="$t('catalogue.category')"
      :help="$t('catalogue.tag_cannot_move')"
    >
      <UInput :model-value="categoryName" disabled class="w-full" />
    </UFormField>

    <UFormField
      v-if="tag"
      :label="$t('catalogue.status')"
      :help="$t('catalogue.tag_status_hint')"
    >
      <USwitch
        v-model="isActive"
        :label="isActive ? $t('catalogue.tag_active') : $t('catalogue.tag_disabled')"
      />
    </UFormField>

    <div class="mt-auto flex gap-2 pt-2">
      <UButton type="button" color="neutral" variant="outline" class="flex-1" justify="center" @click="emit('close')">
        {{ $t('common.cancel') }}
      </UButton>
      <UButton
        type="submit"
        class="flex-1"
        justify="center"
        :loading="busy"
        :disabled="!canSave"
      >
        {{ tag ? $t('catalogue.save_changes') : $t('catalogue.add_tag') }}
      </UButton>
    </div>
  </form>
</template>
