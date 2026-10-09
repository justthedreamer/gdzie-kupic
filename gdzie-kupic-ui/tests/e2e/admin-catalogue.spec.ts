import { test, expect } from '@playwright/test'
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

async function openCatalogue(page: import('@playwright/test').Page, categories: Category[], extra: Parameters<typeof mockApi>[1] = []) {
  await mockApi(page, [
    { method: 'GET', path: '/api/catalogue/categories', respond: () => ({ json: categories }) },
    ...extra,
  ])
  await loginAs(page, 'Admin')
  await page.getByRole('link', { name: 'Katalog' }).click()
  await expect(page).toHaveURL('/admin/catalogue')
}

test('admin sees every category with its tags and the disabled state', async ({ page }) => {
  await openCatalogue(page, seedCategories())

  await expect(page.getByRole('heading', { name: 'Elektronika' })).toBeVisible()
  await expect(page.getByText('Smartfony')).toBeVisible()

  const telewizory = page.getByText('Telewizory').locator('..')
  await expect(telewizory.getByText('Wyłączona')).toBeVisible()

  const moda = page.getByTestId('category-c2')
  await expect(moda.getByText('Wyłączona')).toBeVisible()
})

test('admin creates a category and disables a tag', async ({ page }) => {
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
    {
      method: 'POST',
      path: '/api/admin/tags/t1/disable',
      respond: () => {
        categories[0]!.tags[0]!.isDisabled = true
        return { status: 204 }
      },
    },
  ])

  await page.getByLabel('Nazwa kategorii').fill('Sport')
  await page.getByRole('button', { name: 'Dodaj kategorię' }).click()
  await expect(page.getByRole('heading', { name: 'Sport' })).toBeVisible()

  await page.getByRole('button', { name: 'Wyłącz: Smartfony' }).click()
  await expect(page.getByRole('button', { name: 'Włącz: Smartfony' })).toBeVisible()
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

  await page.getByRole('button', { name: 'Zmień nazwę: Elektronika' }).click()
  await page.getByRole('textbox', { name: 'Elektronika', exact: true }).fill('Moda')
  await page.getByRole('button', { name: 'Zapisz' }).click()

  await expect(page.getByText('Element o takiej nazwie już istnieje.')).toBeVisible()
  // The input stays open so the name can be corrected.
  await page.getByRole('textbox', { name: 'Elektronika', exact: true }).fill('Dom i ogród')
  await page.getByRole('button', { name: 'Zapisz' }).click()

  await expect(page.getByRole('heading', { name: 'Dom i ogród' })).toBeVisible()
})

test('a buyer does not get the catalogue link', async ({ page }) => {
  await loginAs(page, 'Buyer')

  await expect(page.getByRole('link', { name: 'Moje lokalizacje' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Katalog' })).toHaveCount(0)
})
