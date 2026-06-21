<script setup lang="ts">
defineProps<{
  size?: 'sm' | 'md' | 'lg'
}>()
</script>

<template>
  <NuxtLink
    to="/"
    class="inline-flex items-center font-bold tracking-tight select-none"
    :class="{
      'text-base': size === 'sm',
      'text-xl': !size || size === 'md',
      'text-3xl': size === 'lg',
    }"
  >
    <UIcon
      name="i-heroicons-map-pin-solid"
      class="text-primary shrink-0"
      :class="{
        'size-4': size === 'sm',
        'size-5': !size || size === 'md',
        'size-8': size === 'lg',
      }"
    />
    <span>Gdzie<span class="text-primary">Kupić</span></span>
  </NuxtLink>
</template>
