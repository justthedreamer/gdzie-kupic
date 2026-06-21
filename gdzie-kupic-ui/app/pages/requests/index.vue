<script setup lang="ts">
import type { RequestCardProps } from '~/components/RequestCard.vue'

useSeoMeta({ title: 'Zapytania | Gdzie Kupić' })

const requests: RequestCardProps[] = []
</script>

<template>
  <div class="container mx-auto px-4 py-10 max-w-4xl">
    <div class="flex items-center justify-between mb-8">
      <h1 class="text-2xl font-bold">Zapytania</h1>
      <UButton to="/requests/new">
        + Nowe zapytanie
      </UButton>
    </div>

    <div v-if="requests.length" class="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <RequestCard
        v-for="req in requests"
        :key="req.id"
        v-bind="req"
      />
    </div>
    <div v-else class="text-center py-24 text-gray-400">
      Brak zapytań. Bądź pierwszy!
    </div>
  </div>
</template>
