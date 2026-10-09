import type { UserRole } from '~/stores/auth'

declare module '#app' {
  interface PageMeta {
    /** Roles allowed to open the page — enforced by the `role` middleware. */
    roles?: UserRole[]
  }
}

// Use together with the `auth` middleware:
//   definePageMeta({ middleware: ['auth', 'role'], roles: ['Admin'] })
export default defineNuxtRouteMiddleware((to) => {
  const authStore = useAuthStore()
  const roles = to.meta.roles

  if (!roles?.length) return

  if (!authStore.user || !roles.includes(authStore.user.role)) {
    return navigateTo('/')
  }
})
