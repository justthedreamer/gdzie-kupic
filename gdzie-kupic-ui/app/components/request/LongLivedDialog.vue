<script setup lang="ts">
import { LONG_LIVED_DAYS } from '~/utils/posts'

// Offered when nobody was matched: extend the request instead of letting it lapse.
// Any way of closing it other than the accept button counts as dismissing.
defineProps<{
  open: boolean
  busy: boolean
  error: string
}>()

const emit = defineEmits<{ accept: [], dismiss: [] }>()

const days = LONG_LIVED_DAYS
</script>

<template>
  <UModal
    :open="open"
    :title="$t('request.detail.long_lived_title')"
    :description="$t('request.detail.long_lived_text', { days })"
    :dismissible="!busy"
    @update:open="(isOpen: boolean) => { if (!isOpen && !busy) emit('dismiss') }"
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
        <UButton color="neutral" variant="outline" :disabled="busy" @click="emit('dismiss')">
          {{ $t('request.detail.long_lived_dismiss') }}
        </UButton>
        <UButton :loading="busy" @click="emit('accept')">
          {{ $t('request.detail.long_lived_accept', { days }) }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
