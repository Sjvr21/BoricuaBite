export type Address = {
  addressLine1: string
  addressLine2?: string | null
  city: string
  stateOrTerritory: string
  postalCode: string
  country: string
}

export type RestaurantSubscriptionStatus = 'Pending' | 'Active' | 'PastDue' | 'Cancelled'

export type Restaurant = {
  id: string
  name: string
  description: string
  phoneNumber: string
  address: Address
  logoUrl?: string | null
  coverImageUrl?: string | null
  isOpen: boolean
  isPublished?: boolean
  isActive?: boolean
  subscriptionStatus?: RestaurantSubscriptionStatus
  averageRating?: number
  reviewCount?: number
}

export type AdminRestaurant = Restaurant & {
  ownerEmail: string
  stripeConnectedAccountId?: string | null
  stripeSubscriptionId?: string | null
  subscriptionStatus: RestaurantSubscriptionStatus
  isPublished: boolean
  isActive: boolean
}

export type MenuItem = {
  id: string
  name: string
  description: string
  imageUrl?: string | null
  price: number
  isAvailable: boolean
  currency?: string
}

export type Page<T> = {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
}

export type AuthResponse = {
  tokenType: string
  accessToken: string
  expiresIn: number
  refreshToken: string
}

export type Account = {
  id: string
  email: string
  isAdmin: boolean
}

export type MarketplaceOrderStatus = 'Pending' | 'Accepted' | 'Preparing' | 'ReadyForPickup' | 'Completed' | 'Rejected' | 'Cancelled'
export type OrderPaymentMethod = 'Online' | 'PayAtStore'
export type OrderPaymentStatus = 'Pending' | 'Paid' | 'Failed' | 'Refunded'

export type OrderItem = {
  menuItemId: string
  name: string
  unitPrice: number
  quantity: number
  subtotal: number
}

export type Order = {
  id: string
  restaurantId: string
  restaurantName: string
  customerId: string
  customerEmail?: string | null
  status: MarketplaceOrderStatus
  paymentMethod: OrderPaymentMethod
  paymentStatus: OrderPaymentStatus
  subtotal: number
  taxAmount: number
  serviceFee: number
  commissionAmount: number
  total: number
  estimatedRestaurantProceeds: number
  currency: string
  checkoutUrl?: string | null
  createdAtUtc: string
  items: OrderItem[]
}

export type Review = {
  id: string
  restaurantId: string
  orderId: string
  rating: number
  comment?: string | null
  createdAtUtc: string
}

export type ReviewSummary = {
  averageRating: number
  reviewCount: number
}
