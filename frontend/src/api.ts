import type {
  Account,
  AdminRestaurant,
  AuthResponse,
  MarketplaceOrderStatus,
  MenuItem,
  Order,
  OrderPaymentMethod,
  Page,
  Restaurant,
  RestaurantStripeSession,
  RestaurantStripeStatus,
  RestaurantSubscriptionStatus,
  Review,
  ReviewSummary,
  StripeRedirect,
} from './types'

let accessToken = ''

export function setAccessToken(token: string) {
  accessToken = token
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (!headers.has('Content-Type') && options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  const response = await fetch(url, { ...options, headers })
  if (!response.ok) {
    const text = await response.text()
    let message = text || `${response.status} ${response.statusText}`
    try {
      const parsed = JSON.parse(text) as {
        error?: string
        title?: string
        detail?: string
        errors?: Record<string, string[]>
      }
      const validationMessages = parsed.errors
        ? Object.values(parsed.errors).flat().filter(Boolean)
        : []
      message = validationMessages.length > 0
        ? validationMessages.join(' ')
        : parsed.error ?? parsed.detail ?? parsed.title ?? message
    } catch {
      // Keep the raw response.
    }
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T

  const text = await response.text()
  if (!text.trim()) return undefined as T

  return JSON.parse(text) as T
}

export const api = {
  register: (email: string, password: string) =>
    request<void>('/api/auth/register', { method: 'POST', body: JSON.stringify({ email, password }) }),

  startPasswordLogin: (email: string, password: string) =>
    request<{ challengeId: string; expiresInSeconds: number; destination: string; developmentCode?: string | null }>('/api/security/password/start', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  verifyPasswordLogin: (challengeId: string, code: string) =>
    request<AuthResponse>('/api/security/password/verify', {
      method: 'POST',
      body: JSON.stringify({ challengeId, code }),
    }),

  forgotPassword: (email: string) =>
    request<{ message: string; developmentResetCode?: string | null }>('/api/security/password/forgot', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),

  resetPassword: (email: string, resetCode: string, newPassword: string) =>
    request<void>('/api/security/password/reset', {
      method: 'POST',
      body: JSON.stringify({ email, resetCode, newPassword }),
    }),

  googleSignIn: (credential: string) =>
    request<AuthResponse>('/api/security/google', {
      method: 'POST',
      body: JSON.stringify({ credential }),
    }),

  googleLink: (credential: string) =>
    request<{ googleLinked: boolean }>('/api/security/google/link', {
      method: 'POST',
      body: JSON.stringify({ credential }),
    }),

  securityStatus: () => request<{ email2FaAvailable: boolean; googleAvailable: boolean; googleLinked: boolean }>('/api/security/status'),
  account: () => request<Account>('/api/account'),

  browseRestaurants: (search = '', city = '', isOpen?: boolean) => {
    const params = new URLSearchParams({ page: '1', pageSize: '50' })
    if (search.trim()) params.set('search', search.trim())
    if (city.trim()) params.set('city', city.trim())
    if (isOpen !== undefined) params.set('isOpen', String(isOpen))
    return request<Page<Restaurant>>(`/api/restaurants?${params}`)
  },

  publicMenu: (restaurantId: string) =>
    request<Page<MenuItem>>(`/api/restaurants/${restaurantId}/menu?page=1&pageSize=100`),
  publicReviews: (restaurantId: string) =>
    request<Page<Review>>(`/api/restaurants/${restaurantId}/reviews?page=1&pageSize=50`),
  reviewSummary: (restaurantId: string) =>
    request<ReviewSummary>(`/api/restaurants/${restaurantId}/reviews/summary`),

  createOrder: (restaurantId: string, items: { menuItemId: string; quantity: number }[], paymentMethod: OrderPaymentMethod) =>
    request<Order>('/api/orders', {
      method: 'POST',
      body: JSON.stringify({ restaurantId, items, paymentMethod }),
    }),
  customerOrders: () => request<Order[]>('/api/orders/my'),
  createReview: (orderId: string, rating: number, comment: string) =>
    request<Review>('/api/reviews', {
      method: 'POST',
      body: JSON.stringify({ orderId, rating, comment }),
    }),

  ownerRestaurants: () => request<Restaurant[]>('/api/owner/restaurants'),
  updateRestaurant: (restaurantId: string, restaurant: Omit<Restaurant, 'id' | 'isOpen' | 'isPublished' | 'isActive' | 'subscriptionStatus' | 'averageRating' | 'reviewCount'>) =>
    request<Restaurant>(`/api/owner/restaurants/${restaurantId}`, {
      method: 'PUT',
      body: JSON.stringify(restaurant),
    }),
  setRestaurantAvailability: (restaurantId: string, isOpen: boolean) =>
    request<Restaurant>(`/api/owner/restaurants/${restaurantId}/availability`, {
      method: 'PUT',
      body: JSON.stringify({ isOpen }),
    }),

  restaurantStripeStatus: (restaurantId: string) =>
    request<RestaurantStripeStatus>(`/api/owner/restaurants/${restaurantId}/stripe/status`),
  createStripeConnectSession: (restaurantId: string) =>
    request<RestaurantStripeSession>(`/api/owner/restaurants/${restaurantId}/stripe/connect-session`, { method: 'POST' }),
  createStripeExpressDashboard: (restaurantId: string) =>
    request<StripeRedirect>(`/api/owner/restaurants/${restaurantId}/stripe/express-dashboard`, { method: 'POST' }),
  createSubscriptionCheckout: (restaurantId: string) =>
    request<{ sessionId: string; checkoutUrl: string }>(`/api/owner/restaurants/${restaurantId}/stripe/subscription-checkout`, { method: 'POST' }),
  createSubscriptionPortal: (restaurantId: string) =>
    request<{ portalUrl: string }>(`/api/owner/restaurants/${restaurantId}/stripe/subscription-portal`, { method: 'POST' }),

  uploadRestaurantMedia: async (restaurantId: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<{ url: string }>(`/api/owner/restaurants/${restaurantId}/media`, {
      method: 'POST',
      body: form,
    })
  },

  ownerMenu: (restaurantId: string) =>
    request<Page<MenuItem>>(`/api/owner/restaurants/${restaurantId}/menu-items?page=1&pageSize=100`),
  createMenuItem: (restaurantId: string, name: string, price: number, description = '', imageUrl?: string | null) =>
    request<MenuItem>(`/api/owner/restaurants/${restaurantId}/menu-items`, {
      method: 'POST',
      body: JSON.stringify({ name, price, description, imageUrl }),
    }),
  updateMenuItem: (restaurantId: string, itemId: string, item: Pick<MenuItem, 'name' | 'price' | 'description' | 'imageUrl'>) =>
    request<MenuItem>(`/api/owner/restaurants/${restaurantId}/menu-items/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify(item),
    }),
  setMenuItemAvailability: (restaurantId: string, itemId: string, isAvailable: boolean) =>
    request<MenuItem>(`/api/owner/restaurants/${restaurantId}/menu-items/${itemId}/availability`, {
      method: 'PUT',
      body: JSON.stringify({ isAvailable }),
    }),
  deleteMenuItem: (restaurantId: string, itemId: string) =>
    request<void>(`/api/owner/restaurants/${restaurantId}/menu-items/${itemId}`, { method: 'DELETE' }),

  ownerOrders: (restaurantId?: string) => {
    const suffix = restaurantId ? `?restaurantId=${encodeURIComponent(restaurantId)}` : ''
    return request<Order[]>(`/api/owner/orders${suffix}`)
  },
  setOrderStatus: (orderId: string, status: MarketplaceOrderStatus) =>
    request<Order>(`/api/owner/orders/${orderId}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status }),
    }),

  adminRestaurants: () => request<AdminRestaurant[]>('/api/admin/restaurants'),
  adminCreateRestaurant: (ownerEmail: string, restaurant: Omit<Restaurant, 'id' | 'isOpen' | 'isPublished' | 'isActive' | 'subscriptionStatus' | 'averageRating' | 'reviewCount'>) =>
    request<Restaurant>('/api/admin/restaurants', {
      method: 'POST',
      body: JSON.stringify({ ownerEmail, restaurant }),
    }),
  adminSetRestaurantActive: (restaurantId: string, isActive: boolean) =>
    request<AdminRestaurant>(`/api/admin/restaurants/${restaurantId}/active`, {
      method: 'PUT',
      body: JSON.stringify({ isActive }),
    }),
  adminSetSubscription: (
    restaurantId: string,
    status: RestaurantSubscriptionStatus,
    stripeConnectedAccountId?: string | null,
    stripeSubscriptionId?: string | null,
  ) =>
    request<AdminRestaurant>(`/api/admin/restaurants/${restaurantId}/subscription`, {
      method: 'PUT',
      body: JSON.stringify({ status, stripeConnectedAccountId, stripeSubscriptionId }),
    }),
}
