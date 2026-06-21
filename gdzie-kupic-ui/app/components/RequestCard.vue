<script setup lang="ts">
export interface RequestCardProps {
  id: string
  title: string
  category: string
  city: string
  radiusKm: number
  notifiedCount: number
  offersCount: number
  postedAt: string | Date
}

const props = defineProps<RequestCardProps>()

const formattedDate = computed(() =>
  new Intl.DateTimeFormat('pl-PL', { dateStyle: 'medium' }).format(
    new Date(props.postedAt),
  ),
)
</script>

<template>
  <NuxtLink
    :to="`/requests/${id}`"
    class="block rounded-xl border bg-white p-5 shadow-sm hover:shadow-md transition-shadow"
  >
    <!-- Header row -->
    <div class="flex items-start justify-between gap-4">
      <h3 class="font-semibold text-base leading-snug line-clamp-2">
        {{ title }}
      </h3>
      <UBadge variant="soft" color="neutral" class="shrink-0">
        {{ category }}
      </UBadge>
    </div>

    <!-- Meta row -->
    <div class="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-gray-500">
      <span class="flex items-center gap-1">
        <UIcon name="i-heroicons-map-pin" class="size-4" />
        {{ city }} · {{ radiusKm }} km
      </span>
      <span class="flex items-center gap-1">
        <UIcon name="i-heroicons-clock" class="size-4" />
        {{ formattedDate }}
      </span>
    </div>

    <!-- Stats row -->
    <div class="mt-4 flex gap-4 text-sm font-medium">
      <span class="text-gray-500">
        {{ notifiedCount }} powiadomionych
      </span>
      <span class="text-green-600">
        {{ offersCount }} ofert
      </span>
    </div>
  </NuxtLink>
</template>
