<script setup lang="ts">
import { MERCHANT_RESPONSES, RESPONSE_COLOR, type MerchantResponse } from '~/utils/merchantFeed'

defineProps<{
  /** The merchant's current answer, highlighted. */
  current: MerchantResponse | null
  /** The post is closed or an answer is being saved. */
  disabled?: boolean
}>()

defineEmits<{ select: [state: MerchantResponse] }>()

const ICONS: Record<MerchantResponse, string> = {
  HaveIt: 'i-heroicons-check',
  MayHaveIt: 'i-heroicons-clock',
  CanOrderIt: 'i-heroicons-truck',
  CantHelp: 'i-heroicons-x-mark',
}
</script>

<template>
  <div class="flex flex-wrap gap-2">
    <UButton
      v-for="state in MERCHANT_RESPONSES"
      :key="state"
      :color="RESPONSE_COLOR[state]"
      :variant="current === state ? 'solid' : 'outline'"
      :icon="ICONS[state]"
      :aria-label="$t(`merchant_feed.response.${state}`)"
      :aria-pressed="current === state"
      :disabled="disabled"
      @click="$emit('select', state)"
    >
      <span class="hidden sm:inline">{{ $t(`merchant_feed.response.${state}`) }}</span>
    </UButton>
  </div>
</template>
