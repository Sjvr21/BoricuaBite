import type { Account, AuthResponse, MenuItem, Page, Restaurant } from './types'

let accessToken = ''

export function setAccessToken(token: string) {
  accessToken = token
}

async function request<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (!headers.has('Content-Type') && options.body) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  const response = await fetch(url, { ...options, headers })
  if (!response.ok) {
    const text = await response.text()
    throw new Error(text || `${response.status} ${response.statusText}`)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  register: (email: string, password: string) =>
    request<void>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login?useCookies=false', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

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

  ownerRestaurants: () => request<Restaurant[]>('/api/owner/restaurants'),

  createRestaurant: (restaurant: Omit<Restaurant, 'id' | 'isOpen' | 'isActive' | 'logoUrl'>) =>
    request<Restaurant>('/api/owner/restaurants', {
      method: 'POST',
      body: JSON.stringify(restaurant),
    }),

  setRestaurantAvailability: (restaurantId: string, isOpen: boolean) =>
    request<Restaurant>(`/api/owner/restaurants/${restaurantId}/availability`, {
      method: 'PUT',
      body: JSON.stringify({ isOpen }),
    }),

  ownerMenu: (restaurantId: string) =>
    request<Page<MenuItem>>(`/api/owner/restaurants/${restaurantId}/menu-items?page=1&pageSize=100`),

  createMenuItem: (restaurantId: string, name: string, price: number) =>
    request<MenuItem>(`/api/owner/restaurants/${restaurantId}/menu-items`, {
      method: 'POST',
      body: JSON.stringify({ name, price }),
    }),

  setMenuItemAvailability: (restaurantId: string, itemId: string, isAvailable: boolean) =>
    request<MenuItem>(`/api/owner/restaurants/${restaurantId}/menu-items/${itemId}/availability`, {
      method: 'PUT',
      body: JSON.stringify({ isAvailable }),
    }),

  deleteMenuItem: (restaurantId: string, itemId: string) =>
    request<void>(`/api/owner/restaurants/${restaurantId}/menu-items/${itemId}`, {
      method: 'DELETE',
    }),
}
