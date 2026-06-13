<template>
  <q-page class="q-pa-md">
    <div class="row justify-between items-center q-mb-md">
      <div class="text-h5">Active Posts</div>
      <q-btn color="primary" icon="add" label="Create Post" unelevated />
    </div>

    <div class="column q-gutter-md">
      <PostCard
        v-for="post in posts"
        :key="post.id"
        v-bind="post"
        class="cursor-pointer"
      />
    </div>
  </q-page>
</template>

<script setup lang="ts">
import PostCard from 'components/PostCard.vue';
import type { PostCardProps } from 'components/PostCard.vue';

interface Post extends PostCardProps {
  id: number;
}

const posts: Post[] = [
  {
    id: 1,
    title: 'Looking for a plumber',
    description: 'Need someone to fix a leaking pipe under the sink.',
    category: 'Home Services',
    location: 'Warsaw',
    viewCount: 12,
    responseCount: 3,
    timeAgo: '2 hours ago',
  },
  {
    id: 2,
    title: 'Car repair - brake pads',
    description: 'Looking for a mechanic to replace front brake pads on my car.',
    category: 'Automotive',
    location: 'Kraków',
    viewCount: 8,
    responseCount: 1,
    timeAgo: '5 hours ago',
  },
];
</script>
