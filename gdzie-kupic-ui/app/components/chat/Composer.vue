<script setup lang="ts">
import {
  ATTACHMENT_ACCEPT,
  MAX_ATTACHMENT_BYTES,
  MAX_MESSAGE_LENGTH,
  attachmentProblem,
  messageProblem,
  type AttachmentProblem,
} from '~/utils/chat'

// The message box of a thread. Enter sends, Shift+Enter starts a new line. One image (JPEG,
// PNG or WebP, within the size limit) can be attached: it is checked when chosen, shown as
// a preview and can be removed before sending. In a locked thread the box is replaced by the
// explanation. Text and image are cleared only after `send` accepted them (the parent clears
// them through the v-models). `refused` (v-model) is the server's verdict on the image, shown
// until the image is removed or another one is chosen.
const props = defineProps<{ locked: boolean }>()
const text = defineModel<string>({ default: '' })
const image = defineModel<File | null>('image', { default: null })
const refused = defineModel<AttachmentProblem | null>('refused', { default: null })
const emit = defineEmits<{ send: [] }>()

const fileInput = ref<HTMLInputElement | null>(null)
const chosenProblem = ref<AttachmentProblem | null>(null)
const attachmentError = computed(() => chosenProblem.value ?? refused.value)
const previewUrl = ref<string | null>(null)

const problem = computed(() => messageProblem(text.value, image.value !== null))
const length = computed(() => text.value.trim().length)
const showCounter = computed(() => length.value >= MAX_MESSAGE_LENGTH * 0.8)
const tooLong = computed(() => problem.value === 'too_long')

watch(image, (file) => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = file ? URL.createObjectURL(file) : null
}, { immediate: true })

onBeforeUnmount(() => {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
})

function pick() {
  fileInput.value?.click()
}

function onFileChosen(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  // The same file can be chosen again later.
  input.value = ''
  if (!file) return

  refused.value = null
  const rejected = attachmentProblem(file)
  chosenProblem.value = rejected
  if (!rejected) image.value = file
}

function removeImage() {
  image.value = null
  chosenProblem.value = null
  refused.value = null
}

function submit() {
  if (props.locked || problem.value) return
  emit('send')
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault()
    submit()
  }
}
</script>

<template>
  <div class="border-t border-default bg-default p-3">
    <p v-if="locked" class="flex items-center gap-2 text-sm text-muted" data-testid="chat-locked">
      <UIcon name="i-heroicons-lock-closed" class="size-4 shrink-0" />
      {{ $t('chat.locked_notice') }}
    </p>

    <form v-else class="space-y-1" @submit.prevent="submit">
      <div v-if="previewUrl" class="flex items-start gap-2" data-testid="chat-attachment-preview">
        <img :src="previewUrl" :alt="$t('chat.attachment_preview')" class="size-16 rounded-lg border border-default object-cover">
        <UButton
          type="button"
          size="xs"
          color="neutral"
          variant="outline"
          icon="i-heroicons-x-mark"
          :aria-label="$t('chat.attachment_remove')"
          data-testid="chat-attachment-remove"
          @click="removeImage"
        />
      </div>

      <p v-if="attachmentError" class="text-xs text-error" role="alert" data-testid="chat-attachment-error">
        {{ $t(`chat.attachment_${attachmentError === 'unsupported_type' ? 'unsupported' : attachmentError}`, { max: MAX_ATTACHMENT_BYTES / 1024 / 1024 }) }}
      </p>

      <div class="flex items-end gap-2">
        <input
          ref="fileInput"
          type="file"
          class="hidden"
          :accept="ATTACHMENT_ACCEPT"
          tabindex="-1"
          aria-hidden="true"
          data-testid="chat-attach-input"
          @change="onFileChosen"
        >
        <UButton
          type="button"
          color="neutral"
          variant="ghost"
          icon="i-heroicons-photo"
          :aria-label="$t('chat.attach')"
          data-testid="chat-attach"
          @click="pick"
        />
        <UTextarea
          v-model="text"
          class="flex-1"
          :rows="1"
          autoresize
          :maxrows="5"
          :placeholder="$t('chat.composer_placeholder')"
          :aria-label="$t('chat.composer_label')"
          :color="tooLong ? 'error' : undefined"
          data-testid="chat-input"
          @keydown="onKeydown"
        />
        <UButton
          type="submit"
          icon="i-heroicons-paper-airplane"
          :disabled="problem !== null"
          :aria-label="$t('chat.send')"
          data-testid="chat-send"
        />
      </div>

      <p
        v-if="showCounter"
        class="text-right text-xs"
        :class="tooLong ? 'text-error' : 'text-muted'"
        data-testid="chat-counter"
      >
        <span v-if="tooLong" role="alert">{{ $t('chat.too_long', { max: MAX_MESSAGE_LENGTH }) }} </span>{{ length }}/{{ MAX_MESSAGE_LENGTH }}
      </p>
    </form>
  </div>
</template>
