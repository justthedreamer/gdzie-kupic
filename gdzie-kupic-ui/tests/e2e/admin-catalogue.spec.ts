import { test, expect, type Page } from '@playwright/test'
import { loginAs, mockApi } from './support/api-mock'

interface Tag { id: string, name: string, isDisabled: boolean }
interface Category { id: string, name: string, isDisabled: boolean, tags: Tag[] }

function seedCategories(): Category[] {
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
  ]
}

async function openCatalogue(page: Page, categories: Category[], extra: Parameters<typeof mockApi>[1] = []) {
  await mockApi(page, [
    { method: 'GET', path: '/api/catalogue/categories', respond: () => ({ json: categories }) },
    ...extra,
  ])
  // An Admin lands on the catalogue straight after signing in.
  await loginAs(page, 'Admin')
  await expect(page).toHaveURL('/admin/catalogue')
  await expect(page.getByRole('heading', { level: 1, name: 'Zarządzanie katalogiem' })).toBeVisible()
}

/** Desktop shows the category list; below `lg` a picker replaces it. */
async function selectCategory(page: Page, id: string, name: string) {
  if ((page.viewportSize()?.width ?? 0) >= 1024) {
    await page.getByTestId(`category-${id}`).click()
    return
  }

  await page.getByRole('combobox', { name: 'Kategoria' }).click()
  await page.getByRole('option', { name }).click()
}

test('admin lands on the catalogue in the admin shell and sees the first category with its tags', async ({ page }) => {
  await openCatalogue(page, seedCategories())

  await expect(page.getByRole('link', { name: 'Katalog' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Elektronika' })).toBeVisible()
  await expect(page.getByTestId('tag-t1')).toContainText('Smartfony')
  await expect(page.getByTestId('tag-t1')).toContainText('Aktywny')
  await expect(page.getByTestId('tag-t2')).toContainText('Wyłączony')
})

test('admin switches category and sees its disabled state and empty tags', async ({ page }) => {
  await openCatalogue(page, seedCategories())

  await selectCategory(page, 'c2', 'Moda')

  const heading = page.getByRole('heading', { name: 'Moda' })
  await expect(heading).toBeVisible()
  await expect(heading.locator('..').getByText('Wyłączona')).toBeVisible()
  await expect(page.getByText('Brak tagów w tej kategorii.')).toBeVisible()
})

test('admin creates a category from the dialog and it becomes the selected one', async ({ page }) => {
  const categories = seedCategories()

  await openCatalogue(page, categories, [
    {
      method: 'POST',
      path: '/api/admin/categories',
      respond: ({ body }) => {
        const category = { id: 'c3', name: String(body?.name), isDisabled: false, tags: [] }
        categories.push(category)
        return { status: 201, json: category }
      },
    },
  ])

  await page.getByRole('button', { name: 'Dodaj kategorię' }).click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Nazwa kategorii').fill('Sport')
  await dialog.getByRole('button', { name: 'Dodaj kategorię' }).click()

  await expect(dialog).toBeHidden()
  await expect(page.getByRole('heading', { name: 'Sport' })).toBeVisible()
})

test('admin renames a category and a duplicate name shows a readable error', async ({ page }) => {
  const categories = seedCategories()

  await openCatalogue(page, categories, [
    {
      method: 'PUT',
      path: '/api/admin/categories/c1',
      respond: ({ body }) => {
        if (body?.name === 'Moda') return { status: 409, json: { title: 'Conflict' } }
        categories[0]!.name = String(body?.name)
        return { json: categories[0] }
      },
    },
  ])

  await page.getByRole('button', { name: 'Edytuj kategorię' }).click()
  await page.getByLabel('Nazwa kategorii').fill('Moda')
  await page.getByRole('button', { name: 'Zapisz zmiany' }).click()

  await expect(page.getByText('Element o takiej nazwie już istnieje.')).toBeVisible()
  // The input keeps its value so the name can be corrected.
  await expect(page.getByLabel('Nazwa kategorii')).toHaveValue('Moda')

  await page.getByLabel('Nazwa kategorii').fill('Dom i ogród')
  await page.getByRole('button', { name: 'Zapisz zmiany' }).click()

  await expect(page.getByRole('heading', { name: 'Dom i ogród' })).toBeVisible()
})

test('admin edits a tag in the side panel: rename and disable', async ({ page }) => {
  const categories = seedCategories()

  await openCatalogue(page, categories, [
    {
      method: 'PUT',
      path: '/api/admin/tags/t1',
      respond: ({ body }) => {
        categories[0]!.tags[0]!.name = String(body?.name)
        return { json: categories[0]!.tags[0] }
      },
    },
    {
      method: 'POST',
      path: '/api/admin/tags/t1/disable',
      respond: () => {
        categories[0]!.tags[0]!.isDisabled = true
        return { status: 204 }
      },
    },
  ])

  await page.getByRole('button', { name: 'Edytuj tag: Smartfony' }).click()

  const panel = page.getByRole('complementary', { name: 'Edytuj tag' })
  await expect(panel).toBeVisible()
  await panel.getByLabel('Nazwa tagu').fill('Smartfon')
  await panel.getByRole('switch').click()
  await panel.getByRole('button', { name: 'Zapisz zmiany' }).click()

  await expect(panel).toBeHidden()
  await expect(page.getByTestId('tag-t1')).toContainText('Smartfon')
  await expect(page.getByTestId('tag-t1')).toContainText('Wyłączony')
})

test('admin adds a tag to the selected category', async ({ page }) => {
  const categories = seedCategories()

  await openCatalogue(page, categories, [
    {
      method: 'POST',
      path: '/api/admin/categories/c1/tags',
      respond: ({ body }) => {
        const tag = { id: 't3', name: String(body?.name), isDisabled: false }
        categories[0]!.tags.push(tag)
        return { status: 201, json: tag }
      },
    },
  ])

  await page.getByRole('button', { name: 'Dodaj tag', exact: true }).click()
  const panel = page.getByRole('complementary', { name: 'Nowy tag' })
  await panel.getByLabel('Nazwa tagu').fill('Tablety')
  await panel.getByRole('button', { name: 'Dodaj tag' }).click()

  await expect(panel).toBeHidden()
  await expect(page.getByTestId('tag-t3')).toContainText('Tablety')
})

test('admin quickly disables a tag from its row', async ({ page }) => {
  const categories = seedCategories()

  await openCatalogue(page, categories, [
    {
      method: 'POST',
      path: '/api/admin/tags/t1/disable',
      respond: () => {
        categories[0]!.tags[0]!.isDisabled = true
        return { status: 204 }
      },
    },
  ])

  await page.getByRole('button', { name: 'Wyłącz: Smartfony' }).click()
  await expect(page.getByRole('button', { name: 'Włącz: Smartfony' })).toBeVisible()
})

test('a buyer does not get the catalogue link', async ({ page }) => {
  await loginAs(page, 'Buyer')

  await expect(page).toHaveURL('/home')
  await expect(page.getByRole('link', { name: 'Katalog' })).toHaveCount(0)
})
