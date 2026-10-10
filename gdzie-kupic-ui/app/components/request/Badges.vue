<script setup lang="ts">
import type { Post } from '~/composables/api/usePostsApi'
import { POST_STATUS_COLOR } from '~/utils/posts'

// Status plus the urgent / long-lived markers of a request.
defineProps<{ post: Pick<Post, 'status' | 'isUrgent' | 'isLongLived'> }>()
</script>

<template>
  <span class="inline-flex flex-wrap items-center gap-1.5">
    <UBadge :color="POST_STATUS_COLOR[post.status]" variant="subtle" data-testid="post-status">
      {{ $t(`request.status.${post.status}`) }}
    </UBadge>
    <UBadge v-if="post.isUrgent" color="error" variant="subtle" icon="i-heroicons-bolt">
      {{ $t('request.badge.urgent') }}
    </UBadge>
    <UBadge v-if="post.isLongLived" color="info" variant="subtle" icon="i-heroicons-clock">
      {{ $t('request.badge.long_lived') }}
    </UBadge>
  </span>
</template>
