<script setup lang="ts">
import { buyerShell } from '~/utils/shellNav'
</script>

<template>
  <ShellFrame role="Buyer" :config="buyerShell">
    <slot />
  </ShellFrame>
</template>
