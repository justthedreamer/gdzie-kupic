<script setup lang="ts">
import { formatMessageTime } from '~/utils/chat'

// One message bubble: your own on the right, the other side's on the left. A message
// that is still being sent, or failed to send, comes with the retry / discard actions.
// An attached picture opens enlarged on click; the URL the server hands out expires, so a
// picture that fails to load shows a fallback whose button asks the parent for a fresh one.
const props = defineProps<{
  own: boolean
  body: string | null
  /** URL of the attached image (the server's short-lived one, or a local preview while sending). */
  attachmentUrl?: string | null
  createdAt: string
  /** Name of the other side, for screen readers. */
  author: string
  state?: 'sending' | 'failed'
}>()

const emit = defineEmits<{ retry: [], discard: [], reloadImage: [] }>()

const { locale, t } = useI18n()
const time = computed(() => formatMessageTime(props.createdAt, locale.value))

const imageFailed = ref(false)
const imageAttempt = ref(0)
const zoomed = ref(false)
const imageAlt = computed(() => props.own ? t('chat.image_alt_own') : t('chat.image_alt_other', { name: props.author }))

watch(() => props.attachmentUrl, () => {
  imageFailed.value = false
})

function reloadImage() {
  imageFailed.value = false
  imageAttempt.value++
  emit('reloadImage')
}
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

      <template v-if="attachmentUrl">
        <div
          v-if="imageFailed"
          class="flex flex-col items-start gap-2 rounded-lg bg-default/20 p-3 text-sm"
          data-testid="chat-image-fallback"
        >
          <span class="flex items-center gap-1.5">
            <UIcon name="i-heroicons-photo" class="size-4 shrink-0" />
            {{ $t('chat.image_failed') }}
          </span>
          <UButton size="xs" color="neutral" variant="solid" icon="i-heroicons-arrow-path" data-testid="chat-image-reload" @click="reloadImage">
            {{ $t('chat.image_reload') }}
          </UButton>
        </div>
        <button
          v-else
          type="button"
          class="block overflow-hidden rounded-lg"
          :aria-label="$t('chat.image_enlarge')"
          data-testid="chat-image-open"
          @click="zoomed = true"
        >
          <img
            :key="imageAttempt"
            :src="attachmentUrl"
            :alt="imageAlt"
            class="max-h-60 w-full min-w-32 object-cover"
            loading="lazy"
            data-testid="chat-image"
            @error="imageFailed = true"
          >
        </button>

        <UModal v-model:open="zoomed" :title="$t('chat.image_modal_title')" :description="imageAlt">
          <template #body>
            <img :src="attachmentUrl" :alt="imageAlt" class="mx-auto max-h-[70vh] max-w-full rounded-lg object-contain" data-testid="chat-image-large">
          </template>
        </UModal>
      </template>
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
