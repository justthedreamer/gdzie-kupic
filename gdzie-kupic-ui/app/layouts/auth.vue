<template>
  <!-- Minimal layout for auth pages: no header/footer chrome, only the dev account switcher -->
  <div>
    <div class="fixed right-4 top-4 z-50">
      <DevAccountSwitcher />
    </div>
    <slot />
  </div>
</template>
