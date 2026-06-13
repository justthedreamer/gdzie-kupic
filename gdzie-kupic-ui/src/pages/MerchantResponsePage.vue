<template>
  <q-page class="q-pa-md">
    <div class="q-mb-md">
      <q-btn flat icon="arrow_back" label="Back" />
    </div>

    <q-card flat bordered class="q-mb-md">
      <q-card-section>
        <div class="text-h6">{{ response.merchantName }}</div>
        <div class="text-caption text-grey">{{ response.timeAgo }}</div>
      </q-card-section>
      <q-card-section class="q-pt-none">
        <div class="text-body1">{{ response.content }}</div>
      </q-card-section>
      <q-card-actions>
        <q-btn flat color="positive" icon="check_circle" label="Accept & Close Post" />
        <q-btn flat color="negative" icon="cancel" label="Reject" />
      </q-card-actions>
    </q-card>

    <div class="text-h6 q-mb-sm">Conversation</div>

    <div class="column q-gutter-sm q-mb-md">
      <div
        v-for="msg in messages"
        :key="msg.id"
        :class="['q-pa-sm rounded-borders', msg.fromUser ? 'bg-primary text-white self-end' : 'bg-grey-2 self-start']"
        style="max-width: 75%"
      >
        {{ msg.text }}
      </div>
    </div>

    <q-input
      v-model="newMessage"
      outlined
      dense
      placeholder="Write a message..."
      class="q-mt-auto"
    >
      <template #append>
        <q-btn flat round icon="send" color="primary" @click="sendMessage" />
      </template>
    </q-input>
  </q-page>
</template>

<script setup lang="ts">
import { ref } from 'vue';

const response = {
  merchantName: 'Fast Plumbing Co.',
  content: 'We can help! Available tomorrow between 9–12. Price: 150 PLN.',
  timeAgo: '1 hour ago',
};

const messages = ref([
  { id: 1, text: 'Is the price negotiable?', fromUser: true },
  { id: 2, text: 'Yes, we can discuss details. What time works for you?', fromUser: false },
]);

const newMessage = ref('');

function sendMessage() {
  if (!newMessage.value.trim()) return;
  messages.value.push({ id: Date.now(), text: newMessage.value.trim(), fromUser: true });
  newMessage.value = '';
}
</script>
