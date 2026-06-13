<template>
  <q-page class="q-pa-md">
    <div class="row justify-between items-center q-mb-md">
      <div class="text-h5">Posts</div>
      <div class="row q-gutter-sm">
        <q-select
          v-model="filterCategory"
          :options="categories"
          label="Category"
          dense
          outlined
          clearable
          style="min-width: 150px"
        />
        <q-select
          v-model="filterLocation"
          :options="locations"
          label="Location"
          dense
          outlined
          clearable
          style="min-width: 150px"
        />
      </div>
    </div>

    <div class="column q-gutter-md">
      <PostCard
        v-for="post in filteredPosts"
        :key="post.id"
        v-bind="post"
        class="cursor-pointer"
      />
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import PostCard from 'components/PostCard.vue';
import type { PostCardProps } from 'components/PostCard.vue';

interface Post extends PostCardProps {
  id: number;
}

const filterCategory = ref<string | null>(null);
const filterLocation = ref<string | null>(null);

const categories = ['Home Services', 'Automotive', 'Electronics'];
const locations = ['Warsaw', 'Kraków', 'Gdańsk'];

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

const filteredPosts = computed(() =>
  posts.filter(
    (p) =>
      (!filterCategory.value || p.category === filterCategory.value) &&
      (!filterLocation.value || p.location === filterLocation.value),
  ),
);
</script>
