import { FormEvent, useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { Account, MarketplaceOrderStatus, MenuItem, Order, Restaurant } from '../types'

export function OwnerDashboard({
  account,
  run,
  loading,
}: {
  account: Account
  run: (action: () => Promise<void>) => Promise<void>
  loading: boolean
}) {
  const [restaurants, setRestaurants] = useState<Restaurant[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [menu, setMenu] = useState<MenuItem[]>([])
  const [orders, setOrders] = useState<Order[]>([])
  const [profile, setProfile] = useState<Restaurant | null>(null)
  const [dishName, setDishName] = useState('')
  const [dishDescription, setDishDescription] = useState('')
  const [dishPrice, setDishPrice] = useState('')
  const [dishImage, setDishImage] = useState<File | null>(null)

  const selected = useMemo(() => restaurants.find(restaurant => restaurant.id === selectedId) ?? null, [restaurants, selectedId])

  async function refreshRestaurants() {
    const items = await api.ownerRestaurants()
    setRestaurants(items)
    setSelectedId(current => current || items[0]?.id || '')
  }

  async function refreshRestaurantData(restaurantId = selectedId) {
    if (!restaurantId) {
      setMenu([])
      setOrders([])
      return
    }
    const [menuPage, orderItems] = await Promise.all([
      api.ownerMenu(restaurantId),
      api.ownerOrders(restaurantId),
    ])
    setMenu(menuPage.items)
    setOrders(orderItems)
  }

  useEffect(() => { void run(refreshRestaurants) }, [])
  useEffect(() => {
    if (!selectedId) return
    const current = restaurants.find(item => item.id === selectedId) ?? null
    setProfile(current ? structuredClone(current) : null)
    void run(() => refreshRestaurantData(selectedId))
  }, [selectedId])

  useEffect(() => {
    if (selected && profile?.id !== selected.id) setProfile(structuredClone(selected))
  }, [selected])

  async function saveProfile(event: FormEvent) {
    event.preventDefault()
    if (!profile) return
    await run(async () => {
      await api.updateRestaurant(profile.id, {
        name: profile.name,
        description: profile.description,
        phoneNumber: profile.phoneNumber,
        address: profile.address,
        logoUrl: profile.logoUrl,
        coverImageUrl: profile.coverImageUrl,
      })
      await refreshRestaurants()
    })
  }

  async function uploadProfileImage(file: File, kind: 'logo' | 'cover') {
    if (!profile) return
    await run(async () => {
      const uploaded = await api.uploadRestaurantMedia(profile.id, file)
      setProfile(current => current ? {
        ...current,
        logoUrl: kind === 'logo' ? uploaded.url : current.logoUrl,
        coverImageUrl: kind === 'cover' ? uploaded.url : current.coverImageUrl,
      } : current)
    })
  }

  async function addDish(event: FormEvent) {
    event.preventDefault()
    if (!selected) return
    await run(async () => {
      let imageUrl: string | null = null
      if (dishImage) imageUrl = (await api.uploadRestaurantMedia(selected.id, dishImage)).url
      await api.createMenuItem(selected.id, dishName, Number(dishPrice), dishDescription, imageUrl)
      setDishName('')
      setDishDescription('')
      setDishPrice('')
      setDishImage(null)
      await refreshRestaurantData(selected.id)
    })
  }

  async function advanceOrder(order: Order, status: MarketplaceOrderStatus) {
    await run(async () => {
      await api.setOrderStatus(order.id, status)
      await refreshRestaurantData(order.restaurantId)
    })
  }

  return (
    <main className="mx-auto max-w-7xl px-5 py-10">
      <div className="mb-8">
        <p className="text-sm font-bold text-orange-600">Signed in as {account.email}</p>
        <h1 className="text-4xl font-black">Restaurant Dashboard</h1>
        <p className="mt-2 text-stone-600">Manage restaurant branding, menu, availability, and pickup orders.</p>
      </div>

      {restaurants.length === 0 ? (
        <section className="rounded-3xl border border-dashed border-stone-300 bg-white p-10 text-center">
          <h2 className="text-2xl font-black">No restaurant assigned yet</h2>
          <p className="mt-2 text-stone-500">Your account can still order as a customer. Contact BoricuaBite to start a restaurant subscription and onboarding.</p>
        </section>
      ) : (
        <div className="grid gap-6 lg:grid-cols-[300px_1fr]">
          <aside className="h-fit rounded-3xl border border-stone-200 bg-white p-5 lg:sticky lg:top-24">
            <h2 className="font-black">Your restaurants</h2>
            <div className="mt-4 space-y-2">
              {restaurants.map(restaurant => (
                <button key={restaurant.id} onClick={() => setSelectedId(restaurant.id)} className={`w-full rounded-2xl p-3 text-left ${selectedId === restaurant.id ? 'bg-orange-50 text-orange-800' : 'bg-stone-50'}`}>
                  <p className="font-bold">{restaurant.name}</p>
                  <p className="text-xs">{restaurant.subscriptionStatus ?? 'Pending'} · {restaurant.isOpen ? 'Open' : 'Closed'}</p>
                </button>
              ))}
            </div>
          </aside>

          {selected && profile && (
            <div className="space-y-6">
              <section className="overflow-hidden rounded-3xl border border-stone-200 bg-white">
                {profile.coverImageUrl ? <img src={profile.coverImageUrl} alt="" className="h-48 w-full object-cover" /> : <div className="h-36 bg-gradient-to-r from-orange-200 to-amber-100" />}
                <div className="p-6">
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div className="flex items-center gap-4">
                      {profile.logoUrl ? <img src={profile.logoUrl} alt="" className="h-16 w-16 rounded-2xl object-cover" /> : <div className="h-16 w-16 rounded-2xl bg-stone-100" />}
                      <div><h2 className="text-3xl font-black">{profile.name}</h2><p className="text-sm text-stone-500">Subscription: <strong>{profile.subscriptionStatus ?? 'Pending'}</strong></p></div>
                    </div>
                    <button
                      disabled={loading || profile.subscriptionStatus !== 'Active' || profile.isActive === false}
                      onClick={() => void run(async () => { await api.setRestaurantAvailability(profile.id, !profile.isOpen); await refreshRestaurants() })}
                      className={`rounded-2xl px-5 py-3 font-black text-white disabled:bg-stone-300 ${profile.isOpen ? 'bg-stone-700' : 'bg-emerald-600'}`}
                    >
                      {profile.isOpen ? 'Close restaurant' : 'Open restaurant'}
                    </button>
                  </div>
                  {profile.subscriptionStatus !== 'Active' && <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm font-semibold text-amber-800">You can prepare your page and menu now, but the restaurant will not appear in the public catalog or accept orders until the subscription is active.</p>}
                  {profile.isActive === false && <p className="mt-4 rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-700">This listing is suspended by BoricuaBite admin.</p>}
                </div>
              </section>

              <section className="rounded-3xl border border-stone-200 bg-white p-6">
                <h2 className="text-2xl font-black">Restaurant profile</h2>
                <form onSubmit={saveProfile} className="mt-5 grid gap-4 md:grid-cols-2">
                  <Field label="Name"><input required value={profile.name} onChange={event => setProfile({ ...profile, name: event.target.value })} className="input" /></Field>
                  <Field label="Phone"><input required value={profile.phoneNumber} onChange={event => setProfile({ ...profile, phoneNumber: event.target.value })} className="input" /></Field>
                  <Field label="Description" wide><textarea required value={profile.description} onChange={event => setProfile({ ...profile, description: event.target.value })} className="input min-h-24" /></Field>
                  <Field label="Street address"><input required value={profile.address.addressLine1} onChange={event => setProfile({ ...profile, address: { ...profile.address, addressLine1: event.target.value } })} className="input" /></Field>
                  <Field label="City"><input required value={profile.address.city} onChange={event => setProfile({ ...profile, address: { ...profile.address, city: event.target.value } })} className="input" /></Field>
                  <Field label="Postal code"><input required value={profile.address.postalCode} onChange={event => setProfile({ ...profile, address: { ...profile.address, postalCode: event.target.value } })} className="input" /></Field>
                  <Field label="State / territory"><input required value={profile.address.stateOrTerritory} onChange={event => setProfile({ ...profile, address: { ...profile.address, stateOrTerritory: event.target.value } })} className="input" /></Field>
                  <div className="md:col-span-2 grid gap-4 sm:grid-cols-2">
                    <label className="rounded-2xl bg-stone-50 p-4 text-sm font-bold">Upload logo<input type="file" accept="image/jpeg,image/png,image/webp" onChange={event => { const file = event.target.files?.[0]; if (file) void uploadProfileImage(file, 'logo') }} className="mt-2 block w-full text-xs" /></label>
                    <label className="rounded-2xl bg-stone-50 p-4 text-sm font-bold">Upload cover image<input type="file" accept="image/jpeg,image/png,image/webp" onChange={event => { const file = event.target.files?.[0]; if (file) void uploadProfileImage(file, 'cover') }} className="mt-2 block w-full text-xs" /></label>
                  </div>
                  <button className="rounded-2xl bg-stone-900 px-5 py-3 font-black text-white md:col-span-2">Save restaurant profile</button>
                </form>
              </section>

              <section className="rounded-3xl border border-stone-200 bg-white p-6">
                <h2 className="text-2xl font-black">Menu & food photos</h2>
                <form onSubmit={addDish} className="mt-5 grid gap-3 md:grid-cols-2">
                  <input required value={dishName} onChange={event => setDishName(event.target.value)} placeholder="Dish name" className="input" />
                  <input required min="0" step="0.01" type="number" value={dishPrice} onChange={event => setDishPrice(event.target.value)} placeholder="Price" className="input" />
                  <textarea value={dishDescription} onChange={event => setDishDescription(event.target.value)} placeholder="Description" maxLength={1000} className="input min-h-20 md:col-span-2" />
                  <input type="file" accept="image/jpeg,image/png,image/webp" onChange={event => setDishImage(event.target.files?.[0] ?? null)} className="rounded-2xl border border-stone-200 p-3 text-sm md:col-span-2" />
                  <button className="rounded-2xl bg-orange-600 px-5 py-3 font-black text-white md:col-span-2">Add menu item</button>
                </form>

                <div className="mt-6 grid gap-4 md:grid-cols-2">
                  {menu.map(item => (
                    <article key={item.id} className="overflow-hidden rounded-2xl border border-stone-200">
                      {item.imageUrl ? <img src={item.imageUrl} alt={item.name} className="h-36 w-full object-cover" /> : <div className="h-28 bg-stone-100" />}
                      <div className="p-4">
                        <div className="flex justify-between gap-3"><h3 className="font-black">{item.name}</h3><span className="font-black text-orange-600">${item.price.toFixed(2)}</span></div>
                        <p className="mt-1 text-sm text-stone-500">{item.description}</p>
                        <div className="mt-4 flex flex-wrap gap-2"><button onClick={() => void run(async () => { await api.setMenuItemAvailability(selected.id, item.id, !item.isAvailable); await refreshRestaurantData(selected.id) })} className="rounded-xl border border-stone-300 px-3 py-2 text-xs font-bold">{item.isAvailable ? 'Mark sold out' : 'Make available'}</button><button onClick={() => void run(async () => { await api.deleteMenuItem(selected.id, item.id); await refreshRestaurantData(selected.id) })} className="rounded-xl bg-red-50 px-3 py-2 text-xs font-bold text-red-700">Delete</button></div>
                      </div>
                    </article>
                  ))}
                </div>
              </section>

              <section className="rounded-3xl border border-stone-200 bg-white p-6">
                <div className="flex items-center justify-between"><div><p className="text-sm font-bold text-orange-600">Live operations</p><h2 className="text-2xl font-black">Pickup orders</h2></div><button onClick={() => void run(() => refreshRestaurantData(selected.id))} className="rounded-xl bg-stone-100 px-3 py-2 text-sm font-bold">Refresh</button></div>
                <div className="mt-5 space-y-4">
                  {orders.length === 0 ? <p className="text-stone-500">No orders for this restaurant yet.</p> : orders.map(order => <OrderCard key={order.id} order={order} onStatus={status => void advanceOrder(order, status)} />)}
                </div>
              </section>
            </div>
          )}
        </div>
      )}
    </main>
  )
}

function Field({ label, wide = false, children }: { label: string; wide?: boolean; children: React.ReactNode }) {
  return <label className={`block text-sm font-bold ${wide ? 'md:col-span-2' : ''}`}>{label}{children}</label>
}

function OrderCard({ order, onStatus }: { order: Order; onStatus: (status: MarketplaceOrderStatus) => void }) {
  const next = nextStatus(order.status)
  return (
    <article className="rounded-2xl bg-stone-50 p-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div><h3 className="font-black">#{order.id.slice(0, 8)} · {order.customerEmail ?? 'Customer'}</h3><p className="text-sm text-stone-500">{order.paymentMethod} · {order.paymentStatus} · ${order.total.toFixed(2)}</p></div>
        <span className="rounded-full bg-white px-3 py-1 text-sm font-black">{order.status}</span>
      </div>
      <div className="mt-3 text-sm text-stone-700">{order.items.map(item => <p key={item.menuItemId}>{item.quantity} × {item.name}</p>)}</div>
      <div className="mt-4 flex flex-wrap gap-2">
        {order.status === 'Pending' && <button onClick={() => onStatus('Rejected')} className="rounded-xl bg-red-50 px-3 py-2 text-xs font-bold text-red-700">Reject</button>}
        {next && <button onClick={() => onStatus(next)} className="rounded-xl bg-emerald-600 px-3 py-2 text-xs font-bold text-white">{nextLabel(next)}</button>}
      </div>
      <div className="mt-4 grid gap-2 border-t border-stone-200 pt-3 text-xs text-stone-500 sm:grid-cols-3"><span>Tax: ${order.taxAmount.toFixed(2)}</span><span>Commission: ${order.commissionAmount.toFixed(2)}</span><span>Est. proceeds: ${order.estimatedRestaurantProceeds.toFixed(2)}</span></div>
    </article>
  )
}

function nextStatus(status: Order['status']): MarketplaceOrderStatus | null {
  if (status === 'Pending') return 'Accepted'
  if (status === 'Accepted') return 'Preparing'
  if (status === 'Preparing') return 'ReadyForPickup'
  if (status === 'ReadyForPickup') return 'Completed'
  return null
}

function nextLabel(status: MarketplaceOrderStatus) {
  if (status === 'Accepted') return 'Accept order'
  if (status === 'Preparing') return 'Start preparing'
  if (status === 'ReadyForPickup') return 'Mark ready'
  if (status === 'Completed') return 'Complete pickup'
  return status
}
