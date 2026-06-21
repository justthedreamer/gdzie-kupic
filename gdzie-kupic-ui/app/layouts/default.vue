<template>
  <div class="flex flex-col min-h-screen">
    <TheHeader />

    <main class="flex-1">
      <slot />
    </main>

    <footer class="border-t py-6 text-center text-sm text-gray-500">
      &copy; {{ new Date().getFullYear() }} Gdzie Kupić
    </footer>
  </div>
</template>
