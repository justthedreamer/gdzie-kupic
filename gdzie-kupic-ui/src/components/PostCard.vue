<template>
  <q-card flat bordered class="post-card">
    <q-card-section>
      <div class="text-h6">{{ title }}</div>
      <div class="text-caption text-grey q-mt-xs">{{ category }} · {{ location }}</div>
    </q-card-section>

    <q-card-section class="q-pt-none">
      <div class="text-body2">{{ description }}</div>
    </q-card-section>

    <q-card-section class="row items-center q-pt-none text-caption text-grey">
      <q-icon name="visibility" size="xs" class="q-mr-xs" />
      <span class="q-mr-md">{{ viewCount }}</span>
      <q-icon name="reply" size="xs" class="q-mr-xs" />
      <span class="q-mr-md">{{ responseCount }}</span>
      <q-space />
      <span>{{ timeAgo }}</span>
    </q-card-section>
  </q-card>
</template>

<script setup lang="ts">
export interface PostCardProps {
  title: string;
  description: string;
  category: string;
  location: string;
  viewCount: number;
  responseCount: number;
  timeAgo: string;
}

defineProps<PostCardProps>();
</script>
