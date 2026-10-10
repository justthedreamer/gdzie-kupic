<script setup lang="ts">
import type { ShellConfig, ShellNavItem } from '~/utils/shellNav'

const props = defineProps<{
  config: ShellConfig
  /** Counts to show on tabs, by item key. */
  badges?: Record<string, number>
}>()

const { t } = useI18n()

function tabs(items: ShellNavItem[]) {
  return items.map(item => ({ ...item, text: t(item.label), badge: props.badges?.[item.key] ?? 0 }))
}

const groups = computed(() => [tabs(props.config.tabsLeft), tabs(props.config.tabsRight)])
const columns = computed(() =>
  props.config.tabsLeft.length + props.config.tabsRight.length + (props.config.centerAction ? 1 : 0),
)
</script>

<template>
  <nav
    :aria-label="$t('shell.aria_tabs')"
    class="fixed inset-x-0 bottom-0 z-30 border-t border-default bg-default pb-[env(safe-area-inset-bottom)] lg:hidden"
  >
    <ul class="grid items-end" :style="{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }">
      <template v-for="(group, groupIndex) in groups" :key="groupIndex">
        <!-- The optional centre action sits between the two tab groups. -->
        <li v-if="groupIndex === 1 && props.config.centerAction" class="flex justify-center">
          <NuxtLink
            :to="props.config.centerAction.to"
            :aria-label="t(props.config.centerAction.label)"
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
            <span class="relative">
              <UIcon :name="item.icon" class="size-6" />
              <UBadge
                v-if="item.badge"
                color="primary"
                variant="solid"
                size="sm"
                class="absolute -top-1.5 left-3 px-1 py-0 text-[10px]"
                data-testid="nav-badge"
              >
                {{ formatBadgeCount(item.badge) }}
              </UBadge>
            </span>
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
