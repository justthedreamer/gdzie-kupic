import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import { useAuthStore } from '~/stores/auth'
import { useProfileStore } from '~/stores/profile'
import AccountPage from '~/pages/account.vue'

const api = vi.hoisted(() => ({ get: vi.fn(), updateFirstName: vi.fn() }))

mockNuxtImport('useAccountApi', () => () => api)

const profile = (firstName: string | null) => ({ email: 'anna@test', firstName, role: 'Buyer' })

const mounted: Array<{ unmount: () => void }> = []

async function openAccount() {
  const wrapper = await mountSuspended(AccountPage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

type Wrapper = Awaited<ReturnType<typeof openAccount>>

const input = (w: Wrapper) => w.get('[data-testid="account-first-name"]')
const saveButton = (w: Wrapper) => w.get('[data-testid="account-save"]')

async function type(w: Wrapper, value: string) {
  await input(w).setValue(value)
  await flushPromises()
}

describe('account page', () => {
  beforeEach(() => {
    useProfileStore().reset()
    api.get.mockReset().mockResolvedValue(profile('Anna'))
    api.updateFirstName.mockReset().mockImplementation(async (name: string | null) => profile(name))
    useAuthStore().setAuth('token', { id: 'user-1', email: 'anna@test', role: 'Buyer' })
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('shows the e-mail read-only and the current first name', async () => {
    const wrapper = await openAccount()

    expect((wrapper.get('[data-testid="account-email"]').element as HTMLInputElement).value).toBe('anna@test')
    expect(wrapper.get('[data-testid="account-email"]').attributes('disabled')).toBeDefined()
    expect((input(wrapper).element as HTMLInputElement).value).toBe('Anna')
  })

  it('shares the loaded name with the rest of the app', async () => {
    await openAccount()

    expect(useProfileStore().firstName).toBe('Anna')
  })

  it('disables saving until the name changes', async () => {
    const wrapper = await openAccount()
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()

    await type(wrapper, 'Ewa')

    expect(saveButton(wrapper).attributes('disabled')).toBeUndefined()
  })

  it('saves the trimmed name and confirms', async () => {
    const wrapper = await openAccount()
    await type(wrapper, '  Ewa  ')

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(api.updateFirstName).toHaveBeenCalledWith('Ewa')
    expect(wrapper.find('[data-testid="account-saved"]').exists()).toBe(true)
    expect((input(wrapper).element as HTMLInputElement).value).toBe('Ewa')
    expect(useProfileStore().firstName).toBe('Ewa')
  })

  it('clears the name when the field is emptied', async () => {
    const wrapper = await openAccount()
    await type(wrapper, '')

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(api.updateFirstName).toHaveBeenCalledWith(null)
    expect(useProfileStore().firstName).toBeNull()
  })

  it('blocks an invalid name without calling the server', async () => {
    const wrapper = await openAccount()
    await type(wrapper, 'Anna1')

    expect(wrapper.text()).toContain('letters, spaces, hyphens and apostrophes')
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(api.updateFirstName).not.toHaveBeenCalled()
  })

  it('blocks a name that is too long', async () => {
    const wrapper = await openAccount()
    await type(wrapper, 'a'.repeat(51))

    expect(wrapper.text()).toContain('at most 50 characters')
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
  })

  it('shows an inline error when saving fails and keeps the stored name', async () => {
    api.updateFirstName.mockRejectedValue({ response: { status: 500 } })
    const wrapper = await openAccount()
    await type(wrapper, 'Ewa')

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="account-save-error"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="account-saved"]').exists()).toBe(false)
    expect(useProfileStore().firstName).toBe('Anna')
  })

  it('offers a retry when the account cannot be loaded', async () => {
    api.get.mockRejectedValueOnce({ response: { status: 500 } })
    const wrapper = await openAccount()

    expect(wrapper.find('[data-testid="account-load-error"]').exists()).toBe(true)

    await wrapper.get('[data-testid="account-load-error"] button').trigger('click')
    await flushPromises()

    expect((input(wrapper).element as HTMLInputElement).value).toBe('Anna')
  })
})
