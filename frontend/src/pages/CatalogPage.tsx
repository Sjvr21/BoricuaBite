import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { Account, MenuItem, OrderPaymentMethod, Restaurant, Review } from '../types'

export function CatalogPage({
  account,
  onNeedAuth,
  onOrderCreated,
}: {
  account: Account | null
  onNeedAuth: () => void
  onOrderCreated: () => void
}) {
  const [restaurants, setRestaurants] = useState<Restaurant[]>([])
  const [search, setSearch] = useState('')
  const [city, setCity] = useState('')
  const [openOnly, setOpenOnly] = useState(false)
  const [selected, setSelected] = useState<Restaurant | null>(null)
  const [menu, setMenu] = useState<MenuItem[]>([])
  const [reviews, setReviews] = useState<Review[]>([])
  const [cart, setCart] = useState<Record<string, number>>({})
  const [paymentMethod, setPaymentMethod] = useState<OrderPaymentMethod>('PayAtStore')
  const [loading, setLoading] = useState(false)
  const [ordering, setOrdering] = useState(false)
  const [error, setError] = useState('')

  async function load() {
    try {
      setLoading(true)
      setError('')
      const page = await api.browseRestaurants(search, city, openOnly ? true : undefined)
      setRestaurants(page.items)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load restaurants.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  async function openRestaurant(restaurant: Restaurant) {
    try {
      setSelected(restaurant)
      setCart({})
      setError('')
      const [menuPage, reviewPage] = await Promise.all([
        api.publicMenu(restaurant.id),
        api.publicReviews(restaurant.id),
      ])
      setMenu(menuPage.items)
      setReviews(reviewPage.items)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not load restaurant details.')
    }
  }

  const cartRows = useMemo(() => menu
    .filter(item => (cart[item.id] ?? 0) > 0)
    .map(item => ({ item, quantity: cart[item.id], subtotal: item.price * cart[item.id] })), [menu, cart])

  const cartSubtotal = cartRows.reduce((sum, row) => sum + row.subtotal, 0)

  function changeQuantity(itemId: string, delta: number) {
    setCart(current => {
      const next = Math.max(0, Math.min(99, (current[itemId] ?? 0) + delta))
      return { ...current, [itemId]: next }
    })
  }

  async function checkout() {
    if (!account) {
      onNeedAuth()
      return
    }
    if (!selected || cartRows.length === 0) return

    try {
      setOrdering(true)
      setError('')
      const order = await api.createOrder(
        selected.id,
        cartRows.map(row => ({ menuItemId: row.item.id, quantity: row.quantity })),
        paymentMethod,
      )
      if (order.checkoutUrl) {
        window.location.assign(order.checkoutUrl)
        return
      }
      setSelected(null)
      setCart({})
      onOrderCreated()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not place the order.')
    } finally {
      setOrdering(false)
    }
  }

  return (
    <main>
      <section className="bg-gradient-to-br from-orange-700 via-orange-500 to-amber-400 text-white">
        <div className="mx-auto max-w-7xl px-5 py-16 md:py-24">
          <div className="max-w-3xl">
            <p className="mb-3 text-sm font-black uppercase tracking-[0.25em] text-orange-100">Puerto Rico's local marketplace</p>
            <h1 className="text-4xl font-black leading-tight md:text-6xl">Discover, order, and review local food.</h1>
            <p className="mt-5 max-w-2xl text-lg text-orange-50">Menus, verified customer reviews, pickup ordering, and local restaurants in one place.</p>
          </div>

          <div className="mt-8 grid gap-3 rounded-3xl bg-white p-3 shadow-xl md:grid-cols-[1fr_1fr_auto_auto]">
            <input value={search} onChange={event => setSearch(event.target.value)} placeholder="Search restaurants" className="rounded-2xl border border-stone-200 px-4 py-3 text-stone-900 outline-none focus:border-orange-400" />
            <input value={city} onChange={event => setCity(event.target.value)} placeholder="City, e.g. Vega Baja" className="rounded-2xl border border-stone-200 px-4 py-3 text-stone-900 outline-none focus:border-orange-400" />
            <label className="flex items-center gap-2 rounded-2xl px-4 text-sm font-semibold text-stone-700"><input type="checkbox" checked={openOnly} onChange={event => setOpenOnly(event.target.checked)} /> Open now</label>
            <button onClick={() => void load()} className="rounded-2xl bg-stone-900 px-6 py-3 font-bold text-white hover:bg-stone-700">Search</button>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-5 py-10">
        <div className="mb-6 flex items-end justify-between">
          <div><p className="text-sm font-bold uppercase tracking-wider text-orange-600">Restaurants</p><h2 className="text-3xl font-black">Top local choices</h2></div>
          <span className="text-sm text-stone-500">{restaurants.length} found</span>
        </div>
        {error && !selected && <p className="mb-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
        {loading ? <p className="text-stone-500">Loading restaurants...</p> : (
          <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
            {restaurants.map(restaurant => (
              <button key={restaurant.id} onClick={() => void openRestaurant(restaurant)} className="overflow-hidden rounded-3xl border border-stone-200 bg-white text-left shadow-sm transition hover:-translate-y-1 hover:shadow-lg">
                {restaurant.coverImageUrl ? (
                  <img src={restaurant.coverImageUrl} alt="" className="h-44 w-full object-cover" />
                ) : (
                  <div className="h-44 bg-gradient-to-br from-amber-200 to-orange-500" />
                )}
                <div className="p-5">
                  <div className="flex items-start justify-between gap-3">
                    <div><h3 className="text-xl font-black">{restaurant.name}</h3><p className="mt-1 line-clamp-2 text-sm text-stone-600">{restaurant.description}</p></div>
                    {restaurant.logoUrl && <img src={restaurant.logoUrl} alt="" className="h-12 w-12 rounded-xl object-cover" />}
                  </div>
                  <div className="mt-4 flex items-center justify-between text-sm font-semibold text-stone-500">
                    <span>{restaurant.address.city}</span>
                    <span>★ {(restaurant.averageRating ?? 0).toFixed(1)} · {restaurant.reviewCount ?? 0}</span>
                  </div>
                  <span className={`mt-3 inline-block rounded-full px-3 py-1 text-xs font-bold ${restaurant.isOpen ? 'bg-emerald-50 text-emerald-700' : 'bg-stone-100 text-stone-600'}`}>{restaurant.isOpen ? 'Open now' : 'Closed'}</span>
                </div>
              </button>
            ))}
          </div>
        )}
      </section>

      {selected && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 p-0 md:p-8" onClick={() => setSelected(null)}>
          <div onClick={event => event.stopPropagation()} className="mx-auto min-h-full max-w-5xl bg-white md:min-h-0 md:rounded-3xl">
            {selected.coverImageUrl && <img src={selected.coverImageUrl} alt="" className="h-52 w-full object-cover md:rounded-t-3xl" />}
            <div className="p-5 md:p-8">
              <div className="flex items-start justify-between gap-5">
                <div>
                  <h2 className="text-3xl font-black">{selected.name}</h2>
                  <p className="mt-2 max-w-2xl text-stone-600">{selected.description}</p>
                  <p className="mt-2 text-sm font-semibold text-stone-500">★ {(selected.averageRating ?? 0).toFixed(1)} from {selected.reviewCount ?? 0} verified reviews</p>
                </div>
                <button onClick={() => setSelected(null)} className="rounded-full bg-stone-100 px-3 py-2 font-bold">✕</button>
              </div>

              {error && <p className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}

              <div className="mt-8 grid gap-8 lg:grid-cols-[1fr_340px]">
                <div>
                  <h3 className="text-2xl font-black">Menu</h3>
                  <div className="mt-4 grid gap-4 sm:grid-cols-2">
                    {menu.map(item => (
                      <article key={item.id} className="overflow-hidden rounded-2xl border border-stone-200 bg-white">
                        {item.imageUrl ? <img src={item.imageUrl} alt={item.name} className="h-36 w-full object-cover" /> : <div className="h-36 bg-stone-100" />}
                        <div className="p-4">
                          <div className="flex justify-between gap-3"><h4 className="font-black">{item.name}</h4><span className="font-black text-orange-600">${item.price.toFixed(2)}</span></div>
                          <p className="mt-1 min-h-10 text-sm text-stone-500">{item.description}</p>
                          <div className="mt-4 flex items-center justify-between">
                            <span className={`text-xs font-bold ${item.isAvailable ? 'text-emerald-700' : 'text-red-600'}`}>{item.isAvailable ? 'Available' : 'Sold out'}</span>
                            <div className="flex items-center gap-2">
                              {(cart[item.id] ?? 0) > 0 && <button onClick={() => changeQuantity(item.id, -1)} className="h-8 w-8 rounded-full bg-stone-100 font-bold">−</button>}
                              {(cart[item.id] ?? 0) > 0 && <span className="w-5 text-center font-bold">{cart[item.id]}</span>}
                              <button disabled={!item.isAvailable || !selected.isOpen} onClick={() => changeQuantity(item.id, 1)} className="rounded-xl bg-orange-600 px-3 py-2 text-sm font-bold text-white disabled:bg-stone-300">Add</button>
                            </div>
                          </div>
                        </div>
                      </article>
                    ))}
                  </div>

                  <h3 className="mt-10 text-2xl font-black">Verified reviews</h3>
                  <div className="mt-4 space-y-3">
                    {reviews.length === 0 ? <p className="text-stone-500">No verified reviews yet.</p> : reviews.map(review => (
                      <article key={review.id} className="rounded-2xl bg-stone-50 p-4">
                        <div className="font-bold text-amber-600">{'★'.repeat(review.rating)}{'☆'.repeat(5 - review.rating)}</div>
                        {review.comment && <p className="mt-2 text-sm text-stone-700">{review.comment}</p>}
                      </article>
                    ))}
                  </div>
                </div>

                <aside className="h-fit rounded-3xl border border-stone-200 bg-stone-50 p-5 lg:sticky lg:top-24">
                  <h3 className="text-xl font-black">Your order</h3>
                  {cartRows.length === 0 ? <p className="mt-3 text-sm text-stone-500">Add menu items to start an order.</p> : (
                    <>
                      <div className="mt-4 space-y-3">{cartRows.map(row => <div key={row.item.id} className="flex justify-between text-sm"><span>{row.quantity} × {row.item.name}</span><span className="font-bold">${row.subtotal.toFixed(2)}</span></div>)}</div>
                      <div className="mt-4 border-t border-stone-200 pt-4"><div className="flex justify-between font-black"><span>Food subtotal</span><span>${cartSubtotal.toFixed(2)}</span></div><p className="mt-2 text-xs text-stone-500">Tax, service fees, and restaurant commission are calculated and snapshotted by the server when you place the order.</p></div>
                      <div className="mt-5 space-y-2">
                        <label className="flex cursor-pointer gap-2 rounded-xl bg-white p-3 text-sm"><input type="radio" checked={paymentMethod === 'PayAtStore'} onChange={() => setPaymentMethod('PayAtStore')} /> Pay at restaurant</label>
                        <label className="flex cursor-pointer gap-2 rounded-xl bg-white p-3 text-sm"><input type="radio" checked={paymentMethod === 'Online'} onChange={() => setPaymentMethod('Online')} /> Pay online</label>
                      </div>
                      <button disabled={ordering || !selected.isOpen} onClick={() => void checkout()} className="mt-5 w-full rounded-2xl bg-stone-900 px-5 py-3 font-black text-white disabled:opacity-50">{ordering ? 'Placing order...' : account ? 'Place order' : 'Sign in to order'}</button>
                    </>
                  )}
                </aside>
              </div>
            </div>
          </div>
        </div>
      )}
    </main>
  )
}
