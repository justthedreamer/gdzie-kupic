<script setup lang="ts">
// "2 d left" / "3 godz." — the time until an active request expires.
const props = defineProps<{ expiresAt: string }>()

const { t } = useI18n()

const label = computed(() => {
  const remaining = remainingTime(props.expiresAt)

  return remaining
    ? t('request.remaining.left', { time: t(`request.remaining.${remaining.unit}`, { n: remaining.value }) })
    : t('request.remaining.elapsed')
})
</script>

<template>
  <span class="inline-flex items-center gap-1" data-testid="post-remaining">
    <UIcon name="i-heroicons-clock" class="size-4" aria-hidden="true" />
    {{ label }}
  </span>
</template>
