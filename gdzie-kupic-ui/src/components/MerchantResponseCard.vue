<template>
  <q-card flat bordered class="merchant-response-card">
    <q-card-section class="row items-center q-pb-none">
      <div class="text-subtitle2">{{ merchantName }}</div>
      <q-space />
      <q-badge v-if="viewed" color="grey-4" text-color="grey-8" label="Viewed" />
    </q-card-section>

    <q-card-section>
      <div class="text-body2">{{ content }}</div>
    </q-card-section>

    <q-card-section class="q-pt-none text-caption text-grey">
      {{ timeAgo }}
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
export interface MerchantResponseCardProps {
  merchantName: string;
  content: string;
  timeAgo: string;
  viewed: boolean;
}

defineProps<MerchantResponseCardProps>();
</script>
