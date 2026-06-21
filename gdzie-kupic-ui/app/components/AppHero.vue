<script setup lang="ts">
// Props are declared with defineProps — no import needed (auto-imported by Nuxt)
const props = defineProps<{
  title: string
  subtitle: string
  ctaLabel: string
  ctaTo: string
}>()
</script>

<template>
  <section class="py-20 md:py-32 text-center bg-gradient-to-b from-white to-gray-50">
    <div class="container mx-auto px-4 max-w-2xl">
      <h1 class="text-4xl md:text-5xl font-bold tracking-tight leading-tight">
        {{ title }}
      </h1>
      <p class="mt-5 text-lg md:text-xl text-gray-500">
        {{ subtitle }}
      </p>
      <div class="mt-8 flex flex-col sm:flex-row gap-3 justify-center">
        <UButton :to="ctaTo" size="xl">
          {{ ctaLabel }}
        </UButton>
        <UButton to="#how-it-works" variant="ghost" size="xl">
          {{ $t('home.how_it_works') }}
        </UButton>
      </div>
    </div>
  </section>
</template>
