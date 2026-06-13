<template>
  <q-page class="q-pa-md">
    <div class="q-mb-md">
      <q-btn flat icon="arrow_back" label="Back" to="/" />
    </div>

    <q-card flat bordered class="q-mb-md">
      <q-card-section>
        <div class="text-h5">{{ post.title }}</div>
        <div class="text-caption text-grey q-mt-xs">
          {{ post.category }} · {{ post.location }} · {{ post.timeAgo }}
        </div>
      </q-card-section>
      <q-card-section class="q-pt-none">
        <div class="text-body1">{{ post.description }}</div>
      </q-card-section>
      <q-card-section class="row q-pt-none text-caption text-grey">
        <q-icon name="visibility" size="xs" class="q-mr-xs" />
        <span class="q-mr-md">{{ post.viewCount }} views</span>
        <q-icon name="reply" size="xs" class="q-mr-xs" />
        <span>{{ post.responseCount }} responses</span>
      </q-card-section>
    </q-card>

    <div class="text-h6 q-mb-sm">Merchant Responses</div>

    <div class="column q-gutter-md">
      <MerchantResponseCard
        v-for="response in responses"
        :key="response.id"
        v-bind="response"
        class="cursor-pointer"
      />
    </div>
  </q-page>
</template>

<script setup lang="ts">
import MerchantResponseCard from 'components/MerchantResponseCard.vue';
import type { MerchantResponseCardProps } from 'components/MerchantResponseCard.vue';

interface Response extends MerchantResponseCardProps {
  id: number;
}

const post = {
  title: 'Looking for a plumber',
  description: 'Need someone to fix a leaking pipe under the sink.',
  category: 'Home Services',
  location: 'Warsaw',
  viewCount: 12,
  responseCount: 2,
  timeAgo: '2 hours ago',
};

const responses: Response[] = [
  {
    id: 1,
    merchantName: 'Fast Plumbing Co.',
    content: 'We can help! Available tomorrow between 9–12. Price: 150 PLN.',
    timeAgo: '1 hour ago',
    viewed: true,
  },
  {
    id: 2,
    merchantName: 'Pro Fix Services',
    content: 'Hello, we offer same-day service. Please contact us for details.',
    timeAgo: '30 minutes ago',
    viewed: false,
  },
];
</script>
