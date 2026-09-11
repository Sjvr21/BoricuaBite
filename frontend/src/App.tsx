import { useState } from 'react'
import { api, setAccessToken } from './api'
import type { Account } from './types'
import { AuthPage } from './pages/AuthPage'
import { CatalogPage } from './pages/CatalogPage'
import { OrdersPage } from './pages/OrdersPage'
import { OwnerDashboard } from './pages/OwnerDashboard'
import { AdminDashboard } from './pages/AdminDashboard'

type View = 'catalog' | 'auth' | 'orders' | 'owner' | 'admin'

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

  async function login(email: string, password: string) {
    await run(async () => {
      const auth = await api.login(email, password)
      setAccessToken(auth.accessToken)
      const current = await api.account()
      setAccount(current)
      setView('catalog')
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
    <div className="min-h-screen bg-stone-50 text-stone-950">
      <header className="sticky top-0 z-40 border-b border-stone-200 bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-5 py-4">
          <button onClick={() => setView('catalog')} className="text-left">
            <div className="text-2xl font-black tracking-tight text-orange-600">BoricuaBite</div>
            <div className="text-xs font-medium text-stone-500">Local food. Local flavor.</div>
          </button>

          <nav className="flex flex-wrap items-center gap-2">
            <NavButton active={view === 'catalog'} onClick={() => setView('catalog')}>Explore</NavButton>
            {account && <NavButton active={view === 'orders'} onClick={() => setView('orders')}>My Orders</NavButton>}
            {account && !account.isAdmin && <NavButton active={view === 'owner'} onClick={() => setView('owner')}>Restaurant Dashboard</NavButton>}
            {account?.isAdmin && <NavButton active={view === 'admin'} onClick={() => setView('admin')}>Admin</NavButton>}
            {account ? (
              <button onClick={logout} className="rounded-xl px-4 py-2 text-sm font-semibold text-stone-600 hover:bg-stone-100">Sign out</button>
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

      {view === 'catalog' && (
        <CatalogPage
          account={account}
          onNeedAuth={() => setView('auth')}
          onOrderCreated={() => setView('orders')}
        />
      )}
      {view === 'auth' && <AuthPage onLogin={login} loading={loading} run={run} />}
      {view === 'orders' && account && <OrdersPage run={run} />}
      {view === 'owner' && account && !account.isAdmin && <OwnerDashboard account={account} run={run} loading={loading} />}
      {view === 'admin' && account?.isAdmin && <AdminDashboard account={account} run={run} loading={loading} />}
    </div>
  )
}

function NavButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`rounded-xl px-4 py-2 text-sm font-semibold ${active ? 'bg-orange-50 text-orange-700' : 'text-stone-600 hover:bg-stone-100'}`}
    >
      {children}
    </button>
  )
}
