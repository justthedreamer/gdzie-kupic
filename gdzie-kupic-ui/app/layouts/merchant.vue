<script setup lang="ts">
import { merchantShell } from '~/utils/shellNav'
</script>

<template>
  <ShellFrame role="Merchant" :config="merchantShell">
    <slot />
  </ShellFrame>
</template>
