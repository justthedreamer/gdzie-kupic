// /api/posts — Buyer requests ("zapytania"). Contract: planning/phase-4-post-lifecycle-matching.md § API contract.
export type PostStatus = 'Active' | 'Fulfilled' | 'Closed' | 'Expired'
export type NotificationDispatchStatus = 'Pending' | 'Dispatched'

export interface Post {
  id: string
  title: string
  description: string | null
  latitude: number
  longitude: number
  /** `null` = unlimited radius. */
  radiusKm: number | null
  category: { id: string, name: string }
  tag: { id: string, name: string }
  status: PostStatus
  notificationDispatchStatus: NotificationDispatchStatus
  isUrgent: boolean
  urgentDeadline: string | null
  expiresAt: string
  isLongLived: boolean
  createdAt: string
}

/** GET /api/posts item: the post plus how many merchants were notified. */
export interface PostListItem extends Post {
  notifiedCount: number
}

export type PostScope = 'active' | 'ended'

/** GET /api/posts/{id}/status: derived live; the response counts stay 0 until Phase 5. */
export interface PostStatusInfo {
  notificationDispatchStatus: NotificationDispatchStatus
  notifiedCount: number
  checkingCount: number
  haveItCount: number
  mayHaveItCount: number
  canOrderItCount: number
  cannotHelpCount: number
  isZeroMatch: boolean
}

export interface CreatePostRequest {
  latitude: number
  longitude: number
  /** `null` = unlimited radius. */
  radiusKm: number | null
  categoryId: string
  tagId: string
  title: string
  description?: string
  /** ISO timestamp; present = urgent post. */
  urgentDeadline?: string
}

export const usePostsApi = () => {
  const api = useApi()

  return {
    create: (data: CreatePostRequest): Promise<Post> =>
      api.post<Post>('/api/posts', data),

    list: (scope: PostScope): Promise<PostListItem[]> =>
      api.get<PostListItem[]>('/api/posts', { query: { scope } }),

    get: (id: string): Promise<Post> =>
      api.get<Post>(`/api/posts/${id}`),

    status: (id: string): Promise<PostStatusInfo> =>
      api.get<PostStatusInfo>(`/api/posts/${id}/status`),

    /** 204; 409 when the post is no longer active. */
    fulfil: async (id: string): Promise<void> => {
      await api.post(`/api/posts/${id}/fulfil`)
    },

    /** 204; 409 when the post is no longer active. */
    close: async (id: string): Promise<void> => {
      await api.post(`/api/posts/${id}/close`)
    },

    /** Extends the expiry to 14 days; 409 when the post is not eligible. */
    makeLongLived: (id: string): Promise<Post> =>
      api.post<Post>(`/api/posts/${id}/long-lived`),
  }
}
