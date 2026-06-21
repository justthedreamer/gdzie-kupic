<script setup lang="ts">
import type { LocationResponse } from '~/composables/api/useLocationApi'

const confirmedLocation = ref<LocationResponse | null>(null)
</script>

<template>
  <div class="container mx-auto px-4 py-12 max-w-4xl">
    <h1 class="text-3xl font-bold mb-2">Component Test Page</h1>
    <p class="text-gray-500 mb-12">Visual sandbox for developing and reviewing UI components.</p>

    <!-- AppLogo -->
    <section class="mb-12">
      <h2 class="text-lg font-semibold mb-1 text-gray-700">AppLogo</h2>
      <p class="text-sm text-gray-400 mb-4">Reusable site logo with location-pin icon.</p>
      <div class="flex flex-wrap items-end gap-8 p-6 rounded-xl border bg-white">
        <div class="flex flex-col items-center gap-2">
          <AppLogo size="sm" />
          <span class="text-xs text-gray-400">sm</span>
        </div>
        <div class="flex flex-col items-center gap-2">
          <AppLogo size="md" />
          <span class="text-xs text-gray-400">md (default)</span>
        </div>
        <div class="flex flex-col items-center gap-2">
          <AppLogo size="lg" />
          <span class="text-xs text-gray-400">lg</span>
        </div>
      </div>
    </section>

    <!-- LocationPicker -->
    <section class="mb-12">
      <h2 class="text-lg font-semibold mb-1 text-gray-700">LocationPicker</h2>
      <p class="text-sm text-gray-400 mb-4">
        Detects browser GPS, reverse-geocodes via <code class="text-xs">/api/location</code>, and asks the user to confirm.
      </p>
      <div class="p-6 rounded-xl border bg-white max-w-sm">
        <LocationPicker @confirm="confirmedLocation = $event" />
      </div>
      <div v-if="confirmedLocation" class="mt-4 rounded-xl border bg-green-50 p-4 text-sm">
        <span class="font-semibold text-green-700">Confirmed:</span>
        {{ confirmedLocation.city }}, {{ confirmedLocation.postalCode }},
        {{ confirmedLocation.voivodeship }}, {{ confirmedLocation.country }}
      </div>
    </section>
  </div>
</template>
