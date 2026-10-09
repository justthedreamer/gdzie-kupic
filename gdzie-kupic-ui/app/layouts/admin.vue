<script setup lang="ts">
import { adminShell } from '~/utils/shellNav'
</script>

<template>
  <ShellFrame role="Admin" :config="adminShell">
    <slot />
  </ShellFrame>
</template>
