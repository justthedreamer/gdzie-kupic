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
  }
}
