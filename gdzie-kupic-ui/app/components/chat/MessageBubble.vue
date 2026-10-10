<script setup lang="ts">
import { formatMessageTime } from '~/utils/chat'

// One message bubble: your own on the right, the other side's on the left. A message
// that is still being sent, or failed to send, comes with the retry / discard actions.
const props = defineProps<{
  own: boolean
  body: string | null
  createdAt: string
  /** Name of the other side, for screen readers. */
  author: string
  state?: 'sending' | 'failed'
}>()

defineEmits<{ retry: [], discard: [] }>()

const { locale } = useI18n()
const time = computed(() => formatMessageTime(props.createdAt, locale.value))
</script>

<template>
  <li
    class="flex"
    :class="own ? 'justify-end' : 'justify-start'"
    :data-own="own ? 'true' : 'false'"
    :data-state="state ?? 'sent'"
    data-testid="chat-message"
  >
    <div
      class="max-w-[85%] space-y-1 rounded-2xl px-3.5 py-2 sm:max-w-[75%]"
      :class="[
        own ? 'rounded-br-sm bg-primary text-inverted' : 'rounded-bl-sm bg-elevated text-highlighted',
        state === 'sending' ? 'opacity-70' : '',
        state === 'failed' ? 'ring-2 ring-error' : '',
      ]"
    >
      <span class="sr-only">{{ own ? $t('chat.from_you') : $t('chat.from_name', { name: author }) }}: </span>
      <p v-if="body" class="whitespace-pre-wrap break-words text-sm">
        {{ body }}
      </p>

      <p class="text-right text-[11px]" :class="own ? 'text-inverted/80' : 'text-muted'">
        <template v-if="state === 'sending'">
          {{ $t('chat.sending') }}
        </template>
        <template v-else-if="state === 'failed'">
          <span class="font-semibold">{{ $t('chat.failed') }}</span>
        </template>
        <time v-else :datetime="createdAt">{{ time }}</time>
      </p>

      <div v-if="state === 'failed'" class="flex justify-end gap-2">
        <UButton size="xs" color="neutral" variant="solid" icon="i-heroicons-arrow-path" data-testid="chat-retry" @click="$emit('retry')">
          {{ $t('chat.retry_send') }}
        </UButton>
        <UButton size="xs" color="neutral" variant="outline" class="bg-default" data-testid="chat-discard" @click="$emit('discard')">
          {{ $t('chat.discard') }}
        </UButton>
      </div>
    </div>
  </li>
</template>
