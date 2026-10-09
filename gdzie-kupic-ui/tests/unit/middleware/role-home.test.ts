import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import roleHome from '~/middleware/role-home.global'

const navigateTo = vi.hoisted(() => vi.fn())

mockNuxtImport('navigateTo', () => navigateTo)

function visit(path: string) {
  return (roleHome as unknown as (to: unknown, from: unknown) => unknown)({ path }, {})
}

describe('role-home global middleware', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    navigateTo.mockReset()
  })

  it('sends a signed-in Buyer from / to /home', () => {
    useAuthStore().setAuth('t', { id: '1', email: 'b@test', role: 'Buyer' })

    visit('/')

    expect(navigateTo).toHaveBeenCalledWith('/home')
  })

  it.each(['Merchant', 'Admin'] as const)('leaves a %s on /', (role) => {
    useAuthStore().setAuth('t', { id: '1', email: 'x@test', role })

    visit('/')

    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('leaves anonymous visitors on the landing page', () => {
    visit('/')

    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('does not touch other routes', () => {
    useAuthStore().setAuth('t', { id: '1', email: 'b@test', role: 'Buyer' })

    visit('/saved-locations')

    expect(navigateTo).not.toHaveBeenCalled()
  })
})
