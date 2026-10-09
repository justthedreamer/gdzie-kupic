import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import roleMiddleware from '~/middleware/role'

const navigateTo = vi.hoisted(() => vi.fn())

mockNuxtImport('navigateTo', () => navigateTo)

function run(roles?: string[]) {
  return (roleMiddleware as unknown as (to: unknown, from: unknown) => unknown)(
    { meta: { roles } },
    {},
  )
}

describe('role middleware', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    navigateTo.mockReset()
  })

  it('allows a user whose role is listed', () => {
    useAuthStore().setAuth('t', { id: '1', email: 'a@test', role: 'Admin' })

    run(['Admin'])

    expect(navigateTo).not.toHaveBeenCalled()
  })

  it('redirects a user with another role', () => {
    useAuthStore().setAuth('t', { id: '1', email: 'b@test', role: 'Buyer' })

    run(['Admin'])

    expect(navigateTo).toHaveBeenCalledWith('/')
  })

  it('does nothing when the page declares no roles', () => {
    useAuthStore().setAuth('t', { id: '1', email: 'b@test', role: 'Buyer' })

    run(undefined)

    expect(navigateTo).not.toHaveBeenCalled()
  })
})
