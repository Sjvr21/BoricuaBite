import { FormEvent, useEffect, useState } from 'react'
import { api } from '../api'
import type { Account, AdminRestaurant, RestaurantSubscriptionStatus } from '../types'

const emptyRestaurant = {
  name: '',
  description: '',
  phoneNumber: '',
  logoUrl: null as string | null,
  coverImageUrl: null as string | null,
  address: {
    addressLine1: '',
    addressLine2: '',
    city: '',
    stateOrTerritory: 'Puerto Rico',
    postalCode: '',
    country: 'US',
  },
}

export function AdminDashboard({
  account,
  run,
  loading,
}: {
  account: Account
  run: (action: () => Promise<void>) => Promise<void>
  loading: boolean
}) {
  const [restaurants, setRestaurants] = useState<AdminRestaurant[]>([])
  const [ownerEmail, setOwnerEmail] = useState('')
  const [restaurantForm, setRestaurantForm] = useState(emptyRestaurant)

  async function refresh() {
    setRestaurants(await api.adminRestaurants())
  }

  useEffect(() => { void run(refresh) }, [])

  async function createRestaurant(event: FormEvent) {
    event.preventDefault()
    await run(async () => {
      await api.adminCreateRestaurant(ownerEmail, restaurantForm)
      setOwnerEmail('')
      setRestaurantForm(emptyRestaurant)
      await refresh()
    })
  }

  return (
    <main className="mx-auto max-w-7xl px-5 py-10">
      <div className="mb-8">
        <p className="text-sm font-bold text-orange-600">Platform admin · {account.email}</p>
        <h1 className="text-4xl font-black">BoricuaBite Admin</h1>
        <p className="mt-2 max-w-3xl text-stone-600">Onboard restaurant clients, attach their payout account, record subscription state, and suspend listings when necessary.</p>
      </div>

      <div className="grid gap-6 xl:grid-cols-[420px_1fr]">
        <section className="h-fit rounded-3xl border border-stone-200 bg-white p-6 xl:sticky xl:top-24">
          <p className="text-sm font-black uppercase tracking-wider text-orange-600">New client</p>
          <h2 className="text-2xl font-black">Onboard restaurant</h2>
          <p className="mt-2 text-sm text-stone-500">The owner must create their BoricuaBite account first. New restaurants begin with a Pending subscription and stay out of the public catalog until activated.</p>
          <form onSubmit={createRestaurant} className="mt-5 grid gap-3">
            <input required type="email" value={ownerEmail} onChange={event => setOwnerEmail(event.target.value)} placeholder="Owner account email" className="input" />
            <input required value={restaurantForm.name} onChange={event => setRestaurantForm(form => ({ ...form, name: event.target.value }))} placeholder="Restaurant name" className="input" />
            <textarea required value={restaurantForm.description} onChange={event => setRestaurantForm(form => ({ ...form, description: event.target.value }))} placeholder="Restaurant description" className="input min-h-24" />
            <input required value={restaurantForm.phoneNumber} onChange={event => setRestaurantForm(form => ({ ...form, phoneNumber: event.target.value }))} placeholder="Phone number" className="input" />
            <input required value={restaurantForm.address.addressLine1} onChange={event => setRestaurantForm(form => ({ ...form, address: { ...form.address, addressLine1: event.target.value } }))} placeholder="Street address" className="input" />
            <input required value={restaurantForm.address.city} onChange={event => setRestaurantForm(form => ({ ...form, address: { ...form.address, city: event.target.value } }))} placeholder="City" className="input" />
            <input required value={restaurantForm.address.postalCode} onChange={event => setRestaurantForm(form => ({ ...form, address: { ...form.address, postalCode: event.target.value } }))} placeholder="Postal code" className="input" />
            <button disabled={loading} className="rounded-2xl bg-stone-900 px-5 py-3 font-black text-white disabled:opacity-60">Create pending restaurant</button>
          </form>
        </section>

        <section>
          <div className="mb-4 flex items-center justify-between"><div><p className="text-sm font-bold text-orange-600">Restaurant clients</p><h2 className="text-2xl font-black">Subscriptions & payouts</h2></div><button onClick={() => void run(refresh)} className="rounded-xl bg-stone-100 px-4 py-2 text-sm font-bold">Refresh</button></div>
          <div className="space-y-4">
            {restaurants.map(restaurant => (
              <AdminRestaurantCard key={restaurant.id} restaurant={restaurant} loading={loading} run={run} refresh={refresh} />
            ))}
            {restaurants.length === 0 && <div className="rounded-3xl border border-dashed border-stone-300 bg-white p-10 text-center text-stone-500">No restaurant clients yet.</div>}
          </div>
        </section>
      </div>
    </main>
  )
}

function AdminRestaurantCard({
  restaurant,
  loading,
  run,
  refresh,
}: {
  restaurant: AdminRestaurant
  loading: boolean
  run: (action: () => Promise<void>) => Promise<void>
  refresh: () => Promise<void>
}) {
  const [status, setStatus] = useState<RestaurantSubscriptionStatus>(restaurant.subscriptionStatus)
  const [connectedAccount, setConnectedAccount] = useState(restaurant.stripeConnectedAccountId ?? '')
  const [subscriptionId, setSubscriptionId] = useState(restaurant.stripeSubscriptionId ?? '')

  async function saveSubscription() {
    await run(async () => {
      await api.adminSetSubscription(restaurant.id, status, connectedAccount || null, subscriptionId || null)
      await refresh()
    })
  }

  async function toggleActive() {
    await run(async () => {
      await api.adminSetRestaurantActive(restaurant.id, !restaurant.isActive)
      await refresh()
    })
  }

  return (
    <article className="rounded-3xl border border-stone-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2"><h3 className="text-xl font-black">{restaurant.name}</h3><span className={`rounded-full px-2 py-1 text-xs font-bold ${restaurant.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'}`}>{restaurant.isActive ? 'Platform active' : 'Suspended'}</span></div>
          <p className="mt-1 text-sm text-stone-500">Owner: {restaurant.ownerEmail} · {restaurant.address.city}</p>
          <p className="mt-1 text-sm font-semibold">Subscription: {restaurant.subscriptionStatus} · {restaurant.isPublished ? 'Published' : 'Hidden'}</p>
        </div>
        <button onClick={() => void toggleActive()} className={`rounded-xl px-4 py-2 text-sm font-bold ${restaurant.isActive ? 'bg-red-50 text-red-700' : 'bg-emerald-50 text-emerald-700'}`}>{restaurant.isActive ? 'Suspend listing' : 'Restore listing'}</button>
      </div>

      <div className="mt-5 grid gap-3 md:grid-cols-3">
        <label className="text-xs font-bold text-stone-600">Subscription state<select value={status} onChange={event => setStatus(event.target.value as RestaurantSubscriptionStatus)} className="input mt-1"><option>Pending</option><option>Active</option><option>PastDue</option><option>Cancelled</option></select></label>
        <label className="text-xs font-bold text-stone-600">Stripe connected account<input value={connectedAccount} onChange={event => setConnectedAccount(event.target.value)} placeholder="acct_..." className="input mt-1" /></label>
        <label className="text-xs font-bold text-stone-600">Stripe subscription ID<input value={subscriptionId} onChange={event => setSubscriptionId(event.target.value)} placeholder="sub_..." className="input mt-1" /></label>
      </div>
      <p className="mt-3 text-xs text-stone-500">Set Active only after the restaurant's subscription is paid/valid. A connected account is required before customers can choose online payment; pay-at-restaurant can still work without it.</p>
      <button disabled={loading} onClick={() => void saveSubscription()} className="mt-4 rounded-xl bg-orange-600 px-4 py-2 text-sm font-black text-white disabled:opacity-60">Save subscription & payout setup</button>
    </article>
  )
}
