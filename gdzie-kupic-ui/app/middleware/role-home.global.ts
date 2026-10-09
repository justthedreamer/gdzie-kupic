// A signed-in user visiting `/` lands on their role's home page (Buyer -> /home).
// Anonymous visitors, Merchants and Admins keep the public landing page.
export default defineNuxtRouteMiddleware((to) => {
  if (to.path !== '/') return

  const target = homePathFor(useAuthStore().user?.role)
  if (target !== '/') return navigateTo(target)
})
