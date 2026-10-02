<template>
  <div class="flex flex-col min-h-screen">
    <TheHeader />

    <main class="flex-1">
      <slot />
    </main>

    <footer class="border-t border-default py-6 text-center text-sm text-muted">
      &copy; {{ new Date().getFullYear() }} Gdzie Kupić
    </footer>
  </div>
</template>
