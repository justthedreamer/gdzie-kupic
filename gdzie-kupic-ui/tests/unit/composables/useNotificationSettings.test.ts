import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'

const api = vi.hoisted(() => ({ notificationSettings: vi.fn(), updateNotificationSettings: vi.fn() }))

mockNuxtImport('useAccountApi', () => () => api)

beforeEach(() => {
  api.notificationSettings.mockReset().mockResolvedValue({ emailEnabled: false })
  api.updateNotificationSettings.mockReset().mockResolvedValue(undefined)
})

describe('useNotificationSettings', () => {
  it('loads the e-mail setting', async () => {
    api.notificationSettings.mockResolvedValue({ emailEnabled: true })
    const settings = useNotificationSettings()
    expect(settings.loading.value).toBe(true)

    await settings.load()

    expect(settings.loading.value).toBe(false)
    expect(settings.emailEnabled.value).toBe(true)
  })

  it('reports a failed load and can retry', async () => {
    api.notificationSettings.mockRejectedValueOnce(new Error('boom'))
    const settings = useNotificationSettings()

    await settings.load()
    expect(settings.loadFailed.value).toBe(true)

    await settings.load()
    expect(settings.loadFailed.value).toBe(false)
  })

  it('saves a change and keeps the new value', async () => {
    const settings = useNotificationSettings()
    await settings.load()

    expect(await settings.setEmailEnabled(true)).toBe(true)

    expect(api.updateNotificationSettings).toHaveBeenCalledWith({ emailEnabled: true })
    expect(settings.emailEnabled.value).toBe(true)
    expect(settings.saveFailed.value).toBe(false)
  })

  it('keeps the previous value and reports it when saving fails', async () => {
    api.updateNotificationSettings.mockRejectedValue(new Error('boom'))
    const settings = useNotificationSettings()
    await settings.load()

    expect(await settings.setEmailEnabled(true)).toBe(false)

    expect(settings.emailEnabled.value).toBe(false)
    expect(settings.saveFailed.value).toBe(true)
  })

  it('clears the save error on the next attempt', async () => {
    api.updateNotificationSettings.mockRejectedValueOnce(new Error('boom'))
    const settings = useNotificationSettings()
    await settings.load()
    await settings.setEmailEnabled(true)

    await settings.setEmailEnabled(true)

    expect(settings.saveFailed.value).toBe(false)
    expect(settings.emailEnabled.value).toBe(true)
  })

  it('does not save while the setting is not loaded', async () => {
    api.notificationSettings.mockRejectedValue(new Error('boom'))
    const settings = useNotificationSettings()
    await settings.load()

    expect(await settings.setEmailEnabled(true)).toBe(false)
    expect(api.updateNotificationSettings).not.toHaveBeenCalled()
  })
})
