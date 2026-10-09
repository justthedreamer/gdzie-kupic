// /api/catalogue/* (read) and /api/admin/categories|tags/* (Admin management)
export interface CatalogueTag {
  id: string
  name: string
  isDisabled: boolean
}

export interface CatalogueCategory {
  id: string
  name: string
  isDisabled: boolean
  tags: CatalogueTag[]
}

export const useCatalogueApi = () => {
  const api = useApi()

  return {
    getCategories: (): Promise<CatalogueCategory[]> =>
      api.get<CatalogueCategory[]>('/api/catalogue/categories'),

    createCategory: (name: string): Promise<CatalogueCategory> =>
      api.post<CatalogueCategory>('/api/admin/categories', { name }),

    renameCategory: (categoryId: string, name: string): Promise<CatalogueCategory> =>
      api.put<CatalogueCategory>(`/api/admin/categories/${categoryId}`, { name }),

    setCategoryDisabled: (categoryId: string, disabled: boolean): Promise<void> =>
      api.post<undefined>(`/api/admin/categories/${categoryId}/${disabled ? 'disable' : 'enable'}`),

    createTag: (categoryId: string, name: string): Promise<CatalogueTag> =>
      api.post<CatalogueTag>(`/api/admin/categories/${categoryId}/tags`, { name }),

    renameTag: (tagId: string, name: string): Promise<CatalogueTag> =>
      api.put<CatalogueTag>(`/api/admin/tags/${tagId}`, { name }),

    setTagDisabled: (tagId: string, disabled: boolean): Promise<void> =>
      api.post<undefined>(`/api/admin/tags/${tagId}/${disabled ? 'disable' : 'enable'}`),
  }
}
