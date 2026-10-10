<script setup lang="ts">
import type { Post, PostStatusInfo } from '~/composables/api/usePostsApi'

// The status panel of one request: "looking for merchants" while matching is
// pending, then the Live Status counts.
const props = defineProps<{
  post: Pick<Post, 'status'>
  status: PostStatusInfo | null
  /** The first load finished (successfully or not). */
  loaded: boolean
  failed: boolean
}>()

const emit = defineEmits<{ retry: [] }>()

const counts = computed(() => props.status ? statusToLiveCounts(props.status, props.post.status === 'Active') : null)
const pending = computed(() => props.status?.notificationDispatchStatus === 'Pending')
</script>

<template>
  <div class="space-y-3">
    <div v-if="!loaded" class="text-sm text-muted" data-testid="status-loading">
      {{ $t('common.loading') }}
    </div>

    <div v-else-if="!status" class="space-y-3">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-heroicons-exclamation-circle"
        :description="$t('request.detail.status_error')"
      />
      <UButton variant="outline" icon="i-heroicons-arrow-path" @click="emit('retry')">
        {{ $t('common.retry') }}
      </UButton>
    </div>

    <template v-else-if="counts">
      <UAlert
        v-if="pending"
        color="info"
        variant="subtle"
        icon="i-heroicons-magnifying-glass"
        :title="$t('request.detail.matching')"
        :description="$t('request.detail.matching_hint')"
      />
      <UAlert
        v-else-if="status.isZeroMatch"
        color="warning"
        variant="subtle"
        icon="i-heroicons-information-circle"
        :description="$t('request.detail.no_matches')"
      />

      <RequestLiveStatus :request="counts" />

      <p v-if="failed" class="text-xs text-muted">
        {{ $t('request.detail.status_stale') }}
      </p>
    </template>
  </div>
</template>
