import { FormEvent, useState } from 'react'
import { api } from '../api'

export function AuthPage({
  onLogin,
  loading,
  run,
}: {
  onLogin: (email: string, password: string) => Promise<void>
  loading: boolean
  run: (action: () => Promise<void>) => Promise<void>
}) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [note, setNote] = useState('')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (mode === 'login') {
      await onLogin(email, password)
      return
    }

    await run(async () => {
      await api.register(email, password)
      setNote('Account created. You can now sign in and order food. Restaurant clients can use this same account when BoricuaBite assigns their restaurant.')
      setMode('login')
    })
  }

  return (
    <main className="mx-auto grid min-h-[75vh] max-w-5xl items-center gap-10 px-5 py-12 md:grid-cols-2">
      <section>
        <p className="text-sm font-black uppercase tracking-[0.2em] text-orange-600">One BoricuaBite account</p>
        <h1 className="mt-2 text-5xl font-black leading-tight">Order local food or manage your business.</h1>
        <p className="mt-5 text-lg text-stone-600">
          Customers can browse, order, and review. Restaurant owners use the same account after BoricuaBite assigns an approved restaurant subscription.
        </p>
      </section>

      <form onSubmit={submit} className="rounded-3xl border border-stone-200 bg-white p-7 shadow-xl">
        <div className="mb-6 flex rounded-2xl bg-stone-100 p-1">
          <button type="button" onClick={() => setMode('login')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'login' ? 'bg-white shadow-sm' : ''}`}>Sign in</button>
          <button type="button" onClick={() => setMode('register')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'register' ? 'bg-white shadow-sm' : ''}`}>Create account</button>
        </div>

        <label className="mb-2 block text-sm font-bold">Email</label>
        <input type="email" required value={email} onChange={event => setEmail(event.target.value)} className="mb-4 w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />

        <label className="mb-2 block text-sm font-bold">Password</label>
        <input type="password" required minLength={12} value={password} onChange={event => setPassword(event.target.value)} className="w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
        {mode === 'register' && <p className="mt-2 text-xs text-stone-500">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>}
        {note && <p className="mt-3 text-sm text-emerald-700">{note}</p>}

        <button disabled={loading} className="mt-6 w-full rounded-2xl bg-orange-600 px-5 py-3 font-black text-white hover:bg-orange-500 disabled:opacity-60">
          {loading ? 'Working...' : mode === 'login' ? 'Sign in' : 'Create account'}
        </button>
      </form>
    </main>
  )
}
