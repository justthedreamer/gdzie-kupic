<script setup lang="ts">
// A confirmation for destructive / final actions. Closing it any way means "cancel".
defineProps<{
  open: boolean
  title: string
  description: string
  confirmLabel: string
  busy: boolean
  error: string
}>()

const emit = defineEmits<{ confirm: [], cancel: [] }>()
</script>

<template>
  <UModal
    :open="open"
    :title="title"
    :description="description"
    :dismissible="!busy"
    @update:open="(isOpen: boolean) => { if (!isOpen && !busy) emit('cancel') }"
  >
    <template v-if="error" #body>
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="error"
      />
    </template>

    <template #footer>
      <div class="flex w-full justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="busy" @click="emit('cancel')">
          {{ $t('common.cancel') }}
        </UButton>
        <UButton :loading="busy" @click="emit('confirm')">
          {{ confirmLabel }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
