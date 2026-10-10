<script setup lang="ts">
import { MAX_MESSAGE_LENGTH, messageProblem } from '~/utils/chat'

// The message box of a thread. Enter sends, Shift+Enter starts a new line. In a locked
// thread it is replaced by the explanation. The text is cleared only after `send`
// accepted it (the parent clears it through the v-model).
const props = defineProps<{ locked: boolean }>()
const text = defineModel<string>({ default: '' })
const emit = defineEmits<{ send: [] }>()

const problem = computed(() => messageProblem(text.value))
const length = computed(() => text.value.trim().length)
const showCounter = computed(() => length.value >= MAX_MESSAGE_LENGTH * 0.8)
const tooLong = computed(() => problem.value === 'too_long')

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
      <div class="flex items-end gap-2">
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
