<script setup lang="ts">
// One category or tag line of the admin catalogue: name, disabled badge,
// inline rename, and enable/disable. The rename callback resolves to `true`
// on success; on failure the row stays in edit mode so the input is kept.
const props = defineProps<{
  name: string
  isDisabled: boolean
  busy?: boolean
  heading?: boolean
  renameFn: (name: string) => Promise<boolean>
}>()

const emit = defineEmits<{
  toggle: []
}>()

const editing = ref(false)
const draft = ref('')

function startEdit() {
  draft.value = props.name
  editing.value = true
}

async function commit() {
  const name = draft.value.trim()
  if (!name) return

  if (name === props.name || await props.renameFn(name)) {
    editing.value = false
  }
}
</script>

<template>
  <div class="flex items-center justify-between gap-3">
    <form
      v-if="editing"
      class="flex flex-1 items-center gap-2"
      @submit.prevent="commit"
      @keydown.esc="editing = false"
    >
      <UInput
        v-model="draft"
        :aria-label="name"
        maxlength="100"
        autofocus
        class="flex-1"
      />
      <UButton type="submit" size="sm" :loading="busy" :disabled="!draft.trim()">
        {{ $t('common.save') }}
      </UButton>
      <UButton type="button" size="sm" color="neutral" variant="ghost" @click="editing = false">
        {{ $t('common.cancel') }}
      </UButton>
    </form>

    <template v-else>
      <div class="flex min-w-0 items-center gap-2">
        <component
          :is="heading ? 'h2' : 'span'"
          :class="[
            heading ? 'text-base font-semibold' : 'text-sm',
            isDisabled ? 'text-muted line-through' : '',
          ]"
          class="truncate"
        >
          {{ name }}
        </component>
        <UBadge v-if="isDisabled" color="neutral" variant="subtle" size="sm">
          {{ $t('catalogue.disabled') }}
        </UBadge>
      </div>

      <div class="flex shrink-0 items-center gap-1">
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          :aria-label="`${$t('catalogue.rename')}: ${name}`"
          icon="i-heroicons-pencil-square"
          :disabled="busy"
          @click="startEdit"
        />
        <UButton
          size="xs"
          color="neutral"
          variant="ghost"
          :aria-label="`${isDisabled ? $t('catalogue.enable') : $t('catalogue.disable')}: ${name}`"
          :icon="isDisabled ? 'i-heroicons-eye' : 'i-heroicons-eye-slash'"
          :loading="busy"
          @click="emit('toggle')"
        />
      </div>
    </template>
  </div>
</template>
