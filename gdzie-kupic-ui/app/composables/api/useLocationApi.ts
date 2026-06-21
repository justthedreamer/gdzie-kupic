// GET /api/location — resolves GPS coordinates to a human-readable address
export interface LocationRequest {
  latitude: string
  longitude: string
}

export interface LocationResponse {
  voivodeship: string
  postalCode: string
  city: string
  country: string
}

export const useLocationApi = () => {
  const api = useApi()

  return {
    getLocation: (coords: LocationRequest): Promise<LocationResponse> =>
      api.get<LocationResponse>('/api/location', { params: coords }),
  }
}
