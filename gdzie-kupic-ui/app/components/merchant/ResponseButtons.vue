<script setup lang="ts">
import { MERCHANT_RESPONSES, type MerchantResponse } from '~/utils/merchantFeed'

defineProps<{
  /** The merchant's current answer, highlighted. */
  current: MerchantResponse | null
}>()

defineEmits<{ select: [state: MerchantResponse] }>()

const BUTTONS: Record<MerchantResponse, { color: 'success' | 'warning' | 'error', icon: string }> = {
  HaveIt: { color: 'success', icon: 'i-heroicons-check' },
  MayHaveIt: { color: 'warning', icon: 'i-heroicons-clock' },
  CantHelp: { color: 'error', icon: 'i-heroicons-x-mark' },
}
</script>

<template>
  <div class="flex flex-wrap gap-2">
    <UButton
      v-for="state in MERCHANT_RESPONSES"
      :key="state"
      :color="BUTTONS[state].color"
      :variant="current === state ? 'solid' : 'outline'"
      :icon="BUTTONS[state].icon"
      :aria-label="$t(`merchant_feed.response.${state}`)"
      :aria-pressed="current === state"
      @click="$emit('select', state)"
    >
      <span class="hidden sm:inline">{{ $t(`merchant_feed.response.${state}`) }}</span>
    </UButton>
  </div>
</template>
