import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mockNuxtImport, mountSuspended } from '@nuxt/test-utils/runtime'
import { flushPromises } from '@vue/test-utils'
import CataloguePage from '~/pages/admin/catalogue.vue'
import TagEditor from '~/components/catalogue/TagEditor.vue'

const api = vi.hoisted(() => ({
  getCategories: vi.fn(),
  createCategory: vi.fn(),
  renameCategory: vi.fn(),
  setCategoryDisabled: vi.fn(),
  createTag: vi.fn(),
  renameTag: vi.fn(),
  setTagDisabled: vi.fn(),
}))

mockNuxtImport('useCatalogueApi', () => () => api)

type Tag = { id: string, name: string, isDisabled: boolean }
type Category = { id: string, name: string, isDisabled: boolean, tags: Tag[] }

function seed(): Category[] {
  return [
    {
      id: 'c1',
      name: 'Elektronika',
      isDisabled: false,
      tags: [
        { id: 't1', name: 'Smartfony', isDisabled: false },
        { id: 't2', name: 'Telewizory', isDisabled: true },
      ],
    },
    { id: 'c2', name: 'Moda', isDisabled: true, tags: [] },
    {
      id: 'c3',
      name: 'Dom',
      isDisabled: false,
      tags: Array.from({ length: 12 }, (_, i) => ({ id: `d${i}`, name: `Tag ${i + 1}`, isDisabled: false })),
    },
  ]
}

const mounted: Array<{ unmount: () => void }> = []

async function openPage() {
  const wrapper = await mountSuspended(CataloguePage)
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

type Wrapper = Awaited<ReturnType<typeof openPage>>

const rows = (wrapper: Wrapper) => wrapper.findAll('tbody tr')
const button = (wrapper: Wrapper, label: string) =>
  wrapper.findAll('button').find(b => b.attributes('aria-label') === label)

describe('Admin catalogue page', () => {
  beforeEach(() => {
    api.getCategories.mockReset().mockImplementation(async () => seed())
    for (const name of ['createCategory', 'renameCategory', 'setCategoryDisabled', 'createTag', 'renameTag', 'setTagDisabled'] as const) {
      api[name].mockReset().mockResolvedValue(undefined)
    }
  })

  afterEach(() => {
    mounted.splice(0).forEach(wrapper => wrapper.unmount())
  })

  it('lists the categories and shows the first one with its tags', async () => {
    const wrapper = await openPage()

    expect(wrapper.find('[data-testid="category-count"]').text()).toBe('3')
    expect(wrapper.find('[data-testid="category-c1"]').attributes('aria-current')).toBe('true')
    expect(rows(wrapper).map(row => row.find('td').text())).toEqual(['Smartfony', 'Telewizory'])
    expect(wrapper.find('[data-testid="tag-t2"]').text()).toContain('Disabled')
  })

  it('shows another category after selecting it', async () => {
    const wrapper = await openPage()

    await wrapper.find('[data-testid="category-c2"]').trigger('click')

    expect(wrapper.find('[data-testid="category-c2"]').attributes('aria-current')).toBe('true')
    expect(rows(wrapper)).toHaveLength(0)
    expect(wrapper.text()).toContain('No tags in this category.')
  })

  it('paginates the tags of a large category', async () => {
    const wrapper = await openPage()

    await wrapper.find('[data-testid="category-c3"]').trigger('click')

    expect(rows(wrapper)).toHaveLength(10)
    expect(wrapper.find('[data-testid="tag-range"]').text()).toBe('Showing 1 to 10 of 12 tags')
  })

  it('disables a tag from the row and reloads the catalogue', async () => {
    const wrapper = await openPage()

    await button(wrapper, 'Disable: Smartfony')!.trigger('click')
    await flushPromises()

    expect(api.setTagDisabled).toHaveBeenCalledWith('t1', true)
    expect(api.getCategories).toHaveBeenCalledTimes(2)
  })

  it('opens the edit panel and saves a rename and a status change', async () => {
    const wrapper = await openPage()

    await button(wrapper, 'Edit tag: Smartfony')!.trigger('click')
    const panel = wrapper.findComponent(TagEditor)
    expect(panel.exists()).toBe(true)

    await panel.find('input').setValue('Smartfon')
    await panel.find('button[role="switch"]').trigger('click')
    await panel.find('form').trigger('submit')
    await flushPromises()

    expect(api.renameTag).toHaveBeenCalledWith('t1', 'Smartfon')
    expect(api.setTagDisabled).toHaveBeenCalledWith('t1', true)
    expect(wrapper.findComponent(TagEditor).exists()).toBe(false)
  })

  it('keeps the panel and its input open with a readable error on a duplicate name', async () => {
    api.renameTag.mockRejectedValue({ response: { status: 409 }, data: { title: 'Conflict' } })
    const wrapper = await openPage()

    await button(wrapper, 'Edit tag: Smartfony')!.trigger('click')
    const panel = wrapper.findComponent(TagEditor)
    await panel.find('input').setValue('Telewizory')
    await panel.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.findComponent(TagEditor).exists()).toBe(true)
    expect(wrapper.findComponent(TagEditor).text()).toContain('An item with this name already exists.')
    expect((wrapper.findComponent(TagEditor).find('input').element as HTMLInputElement).value).toBe('Telewizory')
    expect(api.setTagDisabled).not.toHaveBeenCalled()
  })

  it('creates a tag in the selected category from "Add tag"', async () => {
    const wrapper = await openPage()

    const addTag = wrapper.findAll('button').find(b => b.text() === 'Add tag')!
    await addTag.trigger('click')

    const panel = wrapper.findComponent(TagEditor)
    expect(panel.text()).toContain('New tag')
    expect(panel.find('button[role="switch"]').exists()).toBe(false)

    await panel.find('input').setValue('Tablet')
    // Regression: the button used to be stuck in the loading state (null === null busy check).
    expect(panel.find('button[type="submit"]').attributes('disabled')).toBeUndefined()
    await panel.find('form').trigger('submit')
    await flushPromises()

    expect(api.createTag).toHaveBeenCalledWith('c1', 'Tablet')
    expect(wrapper.findComponent(TagEditor).exists()).toBe(false)
  })

  it('renames and disables the selected category from its details tab', async () => {
    const wrapper = await openPage()

    await wrapper.findAll('button').find(b => b.text() === 'Edit category')!.trigger('click')
    await flushPromises()

    const form = wrapper.findAll('form').find(f => f.find('input').exists() && f.text().includes('Active tags'))!
    await form.find('input').setValue('Elektronika i AGD')
    await form.find('button[role="switch"]').trigger('click')
    await form.trigger('submit')
    await flushPromises()

    expect(api.renameCategory).toHaveBeenCalledWith('c1', 'Elektronika i AGD')
    expect(api.setCategoryDisabled).toHaveBeenCalledWith('c1', true)
  })

  it('offers a retry when the catalogue cannot be loaded', async () => {
    api.getCategories.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    const wrapper = await openPage()

    expect(wrapper.text()).toContain('Could not load the catalogue.')

    await wrapper.findAll('button').find(b => b.text() === 'Try again')!.trigger('click')
    await flushPromises()

    expect(rows(wrapper)).toHaveLength(2)
  })
})
