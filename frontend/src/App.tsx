import { FormEvent, useEffect, useMemo, useState } from 'react'
import { api, setAccessToken } from './api'
import type { Account, MenuItem, Restaurant } from './types'

type View = 'catalog' | 'auth' | 'owner'

const emptyRestaurant = {
  name: '',
  description: '',
  phoneNumber: '',
  address: {
    addressLine1: '',
    addressLine2: '',
    city: '',
    stateOrTerritory: 'Puerto Rico',
    postalCode: '',
    country: 'US',
  },
}

export default function App() {
  const [view, setView] = useState<View>('catalog')
  const [account, setAccount] = useState<Account | null>(null)
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)

  async function run(action: () => Promise<void>) {
    try {
      setLoading(true)
      setMessage('')
      await action()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Something went wrong.')
    } finally {
      setLoading(false)
    }
  }

  async function handleLogin(email: string, password: string) {
    await run(async () => {
      const auth = await api.login(email, password)
      setAccessToken(auth.accessToken)
      const current = await api.account()
      setAccount(current)
      setView('owner')
      setMessage(`Signed in as ${current.email}`)
    })
  }

  function logout() {
    setAccessToken('')
    setAccount(null)
    setView('catalog')
    setMessage('Signed out.')
  }

  return (
    <div className="min-h-screen bg-stone-50">
      <header className="sticky top-0 z-20 border-b border-stone-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-7xl items-center justify-between px-5 py-4">
          <button onClick={() => setView('catalog')} className="text-left">
            <div className="text-2xl font-black tracking-tight text-orange-600">BoricuaBite</div>
            <div className="text-xs font-medium text-stone-500">Local food. Local flavor.</div>
          </button>
          <nav className="flex items-center gap-2">
            <NavButton active={view === 'catalog'} onClick={() => setView('catalog')}>Explore</NavButton>
            {account ? (
              <>
                <NavButton active={view === 'owner'} onClick={() => setView('owner')}>Owner Dashboard</NavButton>
                <button onClick={logout} className="rounded-xl px-4 py-2 text-sm font-semibold text-stone-600 hover:bg-stone-100">Sign out</button>
              </>
            ) : (
              <button onClick={() => setView('auth')} className="rounded-xl bg-stone-900 px-4 py-2 text-sm font-bold text-white hover:bg-stone-700">Sign in</button>
            )}
          </nav>
        </div>
      </header>

      {message && (
        <div className="mx-auto mt-4 max-w-7xl px-5">
          <div className="rounded-2xl border border-orange-200 bg-orange-50 px-4 py-3 text-sm text-orange-900">{message}</div>
        </div>
      )}

      {view === 'catalog' && <Catalog />}
      {view === 'auth' && <Auth onLogin={handleLogin} loading={loading} run={run} />}
      {view === 'owner' && account && <OwnerDashboard run={run} loading={loading} account={account} />}
    </div>
  )
}

function NavButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button onClick={onClick} className={`rounded-xl px-4 py-2 text-sm font-semibold ${active ? 'bg-orange-50 text-orange-700' : 'text-stone-600 hover:bg-stone-100'}`}>
      {children}
    </button>
  )
}

function Catalog() {
  const [restaurants, setRestaurants] = useState<Restaurant[]>([])
  const [search, setSearch] = useState('')
  const [city, setCity] = useState('')
  const [openOnly, setOpenOnly] = useState(false)
  const [selected, setSelected] = useState<Restaurant | null>(null)
  const [menu, setMenu] = useState<MenuItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  async function load() {
    try {
      setLoading(true)
      setError('')
      const page = await api.browseRestaurants(search, city, openOnly ? true : undefined)
      setRestaurants(page.items)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not load restaurants.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  async function openRestaurant(restaurant: Restaurant) {
    setSelected(restaurant)
    const page = await api.publicMenu(restaurant.id)
    setMenu(page.items)
  }

  return (
    <main>
      <section className="bg-gradient-to-br from-orange-600 via-orange-500 to-amber-400 text-white">
        <div className="mx-auto max-w-7xl px-5 py-16 md:py-24">
          <div className="max-w-3xl">
            <p className="mb-3 text-sm font-black uppercase tracking-[0.25em] text-orange-100">Puerto Rico's local marketplace</p>
            <h1 className="text-4xl font-black leading-tight md:text-6xl">Discover your next favorite local bite.</h1>
            <p className="mt-5 max-w-2xl text-lg text-orange-50">Browse independent restaurants, see what's available, and support local food businesses across Puerto Rico.</p>
          </div>
          <div className="mt-8 grid gap-3 rounded-3xl bg-white p-3 shadow-xl md:grid-cols-[1fr_1fr_auto_auto]">
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search restaurants" className="rounded-2xl border border-stone-200 px-4 py-3 text-stone-900 outline-none focus:border-orange-400" />
            <input value={city} onChange={e => setCity(e.target.value)} placeholder="City, e.g. Vega Baja" className="rounded-2xl border border-stone-200 px-4 py-3 text-stone-900 outline-none focus:border-orange-400" />
            <label className="flex items-center gap-2 rounded-2xl px-4 text-sm font-semibold text-stone-700"><input type="checkbox" checked={openOnly} onChange={e => setOpenOnly(e.target.checked)} /> Open now</label>
            <button onClick={() => void load()} className="rounded-2xl bg-stone-900 px-6 py-3 font-bold text-white hover:bg-stone-700">Search</button>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-5 py-10">
        <div className="mb-6 flex items-end justify-between">
          <div><p className="text-sm font-bold uppercase tracking-wider text-orange-600">Restaurants</p><h2 className="text-3xl font-black">Explore local favorites</h2></div>
          <span className="text-sm text-stone-500">{restaurants.length} found</span>
        </div>
        {error && <p className="mb-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
        {loading ? <p className="text-stone-500">Loading restaurants...</p> : (
          <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
            {restaurants.map(r => (
              <button key={r.id} onClick={() => void openRestaurant(r)} className="overflow-hidden rounded-3xl border border-stone-200 bg-white text-left shadow-sm transition hover:-translate-y-1 hover:shadow-lg">
                <div className="flex h-36 items-end bg-gradient-to-br from-amber-200 to-orange-500 p-5"><span className="rounded-full bg-white/90 px-3 py-1 text-xs font-bold text-stone-800">{r.isOpen ? 'Open now' : 'Closed'}</span></div>
                <div className="p-5"><h3 className="text-xl font-black">{r.name}</h3><p className="mt-1 line-clamp-2 text-sm text-stone-600">{r.description}</p><p className="mt-4 text-sm font-semibold text-stone-500">{r.address.city}, {r.address.stateOrTerritory}</p></div>
              </button>
            ))}
          </div>
        )}
      </section>

      {selected && (
        <div className="fixed inset-0 z-30 flex items-end bg-black/40 md:items-center md:justify-center" onClick={() => setSelected(null)}>
          <div onClick={e => e.stopPropagation()} className="max-h-[85vh] w-full overflow-auto rounded-t-3xl bg-white p-6 md:max-w-2xl md:rounded-3xl">
            <div className="flex items-start justify-between gap-4"><div><h2 className="text-3xl font-black">{selected.name}</h2><p className="mt-2 text-stone-600">{selected.description}</p></div><button onClick={() => setSelected(null)} className="rounded-full bg-stone-100 px-3 py-2 font-bold">✕</button></div>
            <div className="mt-6 space-y-3">{menu.length === 0 ? <p className="text-stone-500">No menu items available yet.</p> : menu.map(item => <div key={item.id} className="flex items-center justify-between rounded-2xl border border-stone-200 p-4"><div><p className="font-bold">{item.name}</p><p className="text-xs text-stone-500">{item.isAvailable ? 'Available' : 'Sold out'}</p></div><p className="font-black text-orange-600">${item.price.toFixed(2)}</p></div>)}</div>
          </div>
        </div>
      )}
    </main>
  )
}

function Auth({ onLogin, loading, run }: { onLogin: (email: string, password: string) => Promise<void>; loading: boolean; run: (action: () => Promise<void>) => Promise<void> }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [note, setNote] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (mode === 'login') return onLogin(email, password)
    await run(async () => {
      await api.register(email, password)
      setNote('Account created. You can sign in now.')
      setMode('login')
    })
  }

  return <main className="mx-auto grid min-h-[75vh] max-w-5xl items-center gap-10 px-5 py-12 md:grid-cols-2">
    <div><p className="text-sm font-black uppercase tracking-[0.2em] text-orange-600">BoricuaBite owners</p><h1 className="mt-2 text-5xl font-black">Manage your restaurant from one place.</h1><p className="mt-5 text-lg text-stone-600">Sign in to manage your restaurant profile, availability, and menu.</p></div>
    <form onSubmit={submit} className="rounded-3xl border border-stone-200 bg-white p-7 shadow-xl">
      <div className="mb-6 flex rounded-2xl bg-stone-100 p-1"><button type="button" onClick={() => setMode('login')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'login' ? 'bg-white shadow-sm' : ''}`}>Sign in</button><button type="button" onClick={() => setMode('register')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'register' ? 'bg-white shadow-sm' : ''}`}>Create account</button></div>
      <label className="mb-2 block text-sm font-bold">Email</label><input type="email" required value={email} onChange={e => setEmail(e.target.value)} className="mb-4 w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
      <label className="mb-2 block text-sm font-bold">Password</label><input type="password" required minLength={12} value={password} onChange={e => setPassword(e.target.value)} className="w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
      {mode === 'register' && <p className="mt-2 text-xs text-stone-500">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>}
      {note && <p className="mt-3 text-sm text-emerald-700">{note}</p>}
      <button disabled={loading} className="mt-6 w-full rounded-2xl bg-orange-600 px-5 py-3 font-black text-white hover:bg-orange-500 disabled:opacity-60">{loading ? 'Working...' : mode === 'login' ? 'Sign in' : 'Create account'}</button>
    </form>
  </main>
}

function OwnerDashboard({ account, run, loading }: { account: Account; run: (action: () => Promise<void>) => Promise<void>; loading: boolean }) {
  const [restaurants, setRestaurants] = useState<Restaurant[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [menu, setMenu] = useState<MenuItem[]>([])
  const [restaurantForm, setRestaurantForm] = useState(emptyRestaurant)
  const [dishName, setDishName] = useState('')
  const [dishPrice, setDishPrice] = useState('')

  const selected = useMemo(() => restaurants.find(r => r.id === selectedId) ?? null, [restaurants, selectedId])

  async function refreshRestaurants() {
    const items = await api.ownerRestaurants()
    setRestaurants(items)
    if (!selectedId && items[0]) setSelectedId(items[0].id)
  }

  async function refreshMenu(id = selectedId) {
    if (!id) return setMenu([])
    const page = await api.ownerMenu(id)
    setMenu(page.items)
  }

  useEffect(() => { void run(refreshRestaurants) }, [])
  useEffect(() => { if (selectedId) void run(() => refreshMenu(selectedId)) }, [selectedId])

  async function createRestaurant(e: FormEvent) {
    e.preventDefault()
    await run(async () => {
      await api.createRestaurant(restaurantForm)
      setRestaurantForm(emptyRestaurant)
      await refreshRestaurants()
    })
  }

  async function addDish(e: FormEvent) {
    e.preventDefault()
    if (!selectedId) return
    await run(async () => {
      await api.createMenuItem(selectedId, dishName, Number(dishPrice))
      setDishName('')
      setDishPrice('')
      await refreshMenu()
    })
  }

  return <main className="mx-auto max-w-7xl px-5 py-10">
    <div className="mb-8"><p className="text-sm font-bold text-orange-600">Signed in as {account.email}</p><h1 className="text-4xl font-black">Owner Dashboard</h1><p className="mt-2 text-stone-600">Manage your restaurants and menu items with the live BoricuaBite API.</p></div>
    <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
      <aside className="space-y-4"><section className="rounded-3xl border border-stone-200 bg-white p-5"><h2 className="font-black">Your restaurants</h2><div className="mt-4 space-y-2">{restaurants.map(r => <button key={r.id} onClick={() => setSelectedId(r.id)} className={`w-full rounded-2xl p-3 text-left ${selectedId === r.id ? 'bg-orange-50 text-orange-800' : 'bg-stone-50'}`}><p className="font-bold">{r.name}</p><p className="text-xs">{r.isOpen ? 'Open' : 'Closed'} · {r.address.city}</p></button>)}</div></section></aside>
      <div className="space-y-6">
        {selected ? <>
          <section className="rounded-3xl border border-stone-200 bg-white p-6"><div className="flex flex-wrap items-center justify-between gap-4"><div><p className="text-sm font-bold text-orange-600">Restaurant</p><h2 className="text-3xl font-black">{selected.name}</h2><p className="mt-1 text-stone-500">{selected.address.city}, {selected.address.stateOrTerritory}</p></div><button disabled={loading} onClick={() => void run(async () => { await api.setRestaurantAvailability(selected.id, !selected.isOpen); await refreshRestaurants() })} className={`rounded-2xl px-5 py-3 font-black text-white ${selected.isOpen ? 'bg-stone-700' : 'bg-emerald-600'}`}>{selected.isOpen ? 'Close restaurant' : 'Open restaurant'}</button></div></section>
          <section className="rounded-3xl border border-stone-200 bg-white p-6"><h2 className="text-2xl font-black">Menu</h2><form onSubmit={addDish} className="mt-4 grid gap-3 md:grid-cols-[1fr_160px_auto]"><input required value={dishName} onChange={e => setDishName(e.target.value)} placeholder="Dish name" className="rounded-2xl border border-stone-200 px-4 py-3" /><input required min="0" step="0.01" type="number" value={dishPrice} onChange={e => setDishPrice(e.target.value)} placeholder="Price" className="rounded-2xl border border-stone-200 px-4 py-3" /><button className="rounded-2xl bg-orange-600 px-5 py-3 font-bold text-white">Add dish</button></form><div className="mt-5 space-y-3">{menu.map(item => <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-stone-50 p-4"><div><p className="font-bold">{item.name}</p><p className="text-sm text-stone-500">${item.price.toFixed(2)} · {item.isAvailable ? 'Available' : 'Sold out'}</p></div><div className="flex gap-2"><button onClick={() => void run(async () => { await api.setMenuItemAvailability(selected.id, item.id, !item.isAvailable); await refreshMenu() })} className="rounded-xl border border-stone-300 px-3 py-2 text-sm font-semibold">{item.isAvailable ? 'Mark sold out' : 'Make available'}</button><button onClick={() => void run(async () => { await api.deleteMenuItem(selected.id, item.id); await refreshMenu() })} className="rounded-xl bg-red-50 px-3 py-2 text-sm font-semibold text-red-700">Delete</button></div></div>)}</div></section>
        </> : <section className="rounded-3xl border border-dashed border-stone-300 bg-white p-8 text-center text-stone-500">Create or select a restaurant to manage it.</section>}

        <section className="rounded-3xl border border-stone-200 bg-white p-6"><h2 className="text-2xl font-black">Add a restaurant</h2><form onSubmit={createRestaurant} className="mt-4 grid gap-3 md:grid-cols-2">{[
          ['name','Restaurant name'],['description','Description'],['phoneNumber','Phone number'],['address.addressLine1','Address'],['address.city','City'],['address.postalCode','Postal code']
        ].map(([key,label]) => <input key={key} required placeholder={label} value={key.includes('.') ? (restaurantForm.address as Record<string,string>)[key.split('.')[1]] : (restaurantForm as unknown as Record<string,string>)[key]} onChange={e => { const value=e.target.value; if (key.includes('.')) { const k=key.split('.')[1]; setRestaurantForm(f => ({...f,address:{...f.address,[k]:value}})) } else setRestaurantForm(f => ({...f,[key]:value})) }} className="rounded-2xl border border-stone-200 px-4 py-3" />)}<button className="rounded-2xl bg-stone-900 px-5 py-3 font-black text-white md:col-span-2">Create restaurant</button></form></section>
      </div>
    </div>
  </main>
}
