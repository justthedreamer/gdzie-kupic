// /api/saved-locations — Buyer saved locations
export interface SavedLocation {
  id: string
  displayName: string
  latitude: number
  longitude: number
  createdAt: string
}

export type CreateSavedLocationRequest = { displayName: string } & LocationPayload

export const useSavedLocationsApi = () => {
  const api = useApi()

  return {
    list: (): Promise<SavedLocation[]> =>
      api.get<SavedLocation[]>('/api/saved-locations'),

    create: (data: CreateSavedLocationRequest): Promise<SavedLocation> =>
      api.post<SavedLocation>('/api/saved-locations', data),

    remove: (id: string): Promise<void> =>
      api.del<undefined>(`/api/saved-locations/${id}`),
  }
}
