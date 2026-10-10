<script setup lang="ts">
import type { PostListItem } from '~/composables/api/usePostsApi'

const props = defineProps<{ post: PostListItem }>()

const category = computed(() => `${props.post.category.name} · ${props.post.tag.name}`)
</script>

<template>
  <NuxtLink
    :to="`/requests/${post.id}`"
    class="block rounded-lg border border-default bg-default p-5 shadow-[var(--shadow-card)] transition-shadow hover:shadow-[var(--shadow-hover)] focus-visible:outline-2 focus-visible:outline-primary"
    data-testid="request-card"
  >
    <div class="flex items-start justify-between gap-3">
      <h2 class="line-clamp-2 text-base font-semibold leading-snug text-highlighted">
        {{ post.title }}
      </h2>
      <RequestBadges :post="post" class="shrink-0 justify-end" />
    </div>

    <p class="mt-2 text-sm text-muted">
      {{ category }}
    </p>

    <div class="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted">
      <span class="flex items-center gap-1">
        <UIcon name="i-heroicons-bell-alert" class="size-4" aria-hidden="true" />
        {{ $t('request.list.notified', { count: post.notifiedCount }) }}
      </span>
      <RequestRemaining v-if="post.status === 'Active'" :expires-at="post.expiresAt" />
    </div>
  </NuxtLink>
</template>
