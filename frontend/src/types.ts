export type Address = {
  addressLine1: string
  addressLine2?: string | null
  city: string
  stateOrTerritory: string
  postalCode: string
  country: string
}

export type Restaurant = {
  id: string
  name: string
  description: string
  phoneNumber: string
  address: Address
  isOpen: boolean
  isActive?: boolean
  logoUrl?: string | null
}

export type MenuItem = {
  id: string
  name: string
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
}
