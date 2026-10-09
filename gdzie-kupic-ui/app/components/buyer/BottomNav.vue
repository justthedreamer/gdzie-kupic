<script setup lang="ts">
import { buyerTabsLeft, buyerTabsRight, type BuyerNavItem } from '~/utils/buyerNav'

const { t } = useI18n()

function tabs(items: BuyerNavItem[]) {
  return items.map(item => ({ ...item, text: t(item.label) }))
}

const left = computed(() => tabs(buyerTabsLeft))
const right = computed(() => tabs(buyerTabsRight))
</script>

<template>
  <nav
    :aria-label="$t('buyer_nav.aria_tabs')"
    class="fixed inset-x-0 bottom-0 z-30 border-t border-default bg-default pb-[env(safe-area-inset-bottom)] lg:hidden"
  >
    <ul class="grid grid-cols-5 items-end">
      <template v-for="(group, groupIndex) in [left, right]" :key="groupIndex">
        <!-- The centre "new request" action sits between the two tab groups. -->
        <li v-if="groupIndex === 1" class="flex justify-center">
          <NuxtLink
            :to="BUYER_NEW_REQUEST_PATH"
            :aria-label="$t('request.new')"
            class="-mt-5 flex size-14 items-center justify-center rounded-full bg-primary text-white shadow-lg"
          >
            <UIcon name="i-heroicons-plus" class="size-7" />
          </NuxtLink>
        </li>

        <li v-for="item in group" :key="item.key">
          <NuxtLink
            v-if="item.to"
            :to="item.to"
            class="flex flex-col items-center gap-0.5 py-2 text-xs text-muted"
            active-class="font-semibold text-primary"
          >
            <UIcon :name="item.icon" class="size-6" />
            {{ item.text }}
          </NuxtLink>

          <button
            v-else
            type="button"
            disabled
            class="flex w-full cursor-not-allowed flex-col items-center gap-0.5 py-2 text-xs text-dimmed"
          >
            <UIcon :name="item.icon" class="size-6" />
            {{ item.text }}
          </button>
        </li>
      </template>
    </ul>
  </nav>
</template>
