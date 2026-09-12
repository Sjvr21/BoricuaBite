import { useEffect, useRef, useState } from 'react'
import { api } from '../api'
import type { Account } from '../types'

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (options: { client_id: string; callback: (response: { credential: string }) => void }) => void
          renderButton: (element: HTMLElement, options: Record<string, unknown>) => void
        }
      }
    }
  }
}

export function SecurityPage({ account, run }: { account: Account; run: (action: () => Promise<void>) => Promise<void> }) {
  const [status, setStatus] = useState<{ email2FaAvailable: boolean; googleAvailable: boolean; googleLinked: boolean } | null>(null)
  const [note, setNote] = useState('')
  const googleButton = useRef<HTMLDivElement>(null)
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

  useEffect(() => {
    void run(async () => setStatus(await api.securityStatus()))
  }, [run])

  useEffect(() => {
    if (!clientId || status?.googleLinked || !status?.googleAvailable || !googleButton.current) return
    const render = () => {
      if (!window.google || !googleButton.current) return
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: response => {
          void run(async () => {
            await api.googleLink(response.credential)
            setStatus(await api.securityStatus())
            setNote('Google account linked. You can now use Continue with Google to sign in.')
          })
        },
      })
      googleButton.current.innerHTML = ''
      window.google.accounts.id.renderButton(googleButton.current, { theme: 'outline', size: 'large', shape: 'pill', text: 'continue_with' })
    }
    if (window.google) return render()
    const script = document.createElement('script')
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.defer = true
    script.onload = render
    document.head.appendChild(script)
    return () => script.remove()
  }, [clientId, status, run])

  return (
    <main className="mx-auto max-w-3xl px-5 py-10">
      <h1 className="text-4xl font-black">Security</h1>
      <p className="mt-2 text-stone-600">Manage sign-in protection for {account.email}.</p>

      <section className="mt-8 rounded-3xl border border-stone-200 bg-white p-6 shadow-sm">
        <h2 className="text-xl font-black">Email two-factor authentication</h2>
        <p className="mt-2 text-sm text-stone-600">Password sign-ins require a six-digit one-time code after your password is verified.</p>
        <div className="mt-4 inline-flex rounded-full bg-stone-100 px-3 py-1 text-sm font-bold">
          {status?.email2FaAvailable ? 'Email delivery configured' : 'Development fallback / email provider not configured'}
        </div>
      </section>

      <section className="mt-5 rounded-3xl border border-stone-200 bg-white p-6 shadow-sm">
        <h2 className="text-xl font-black">Google</h2>
        <p className="mt-2 text-sm text-stone-600">Link the Google account that uses the same email address. BoricuaBite stores Google's stable external account identifier, not your Google password.</p>
        <div className="mt-4">
          {status?.googleLinked ? (
            <span className="inline-flex rounded-full bg-emerald-50 px-3 py-1 text-sm font-bold text-emerald-700">Google linked</span>
          ) : status?.googleAvailable && clientId ? (
            <div ref={googleButton} />
          ) : (
            <span className="inline-flex rounded-full bg-amber-50 px-3 py-1 text-sm font-bold text-amber-700">Google sign-in not configured yet</span>
          )}
        </div>
        {note && <p className="mt-3 text-sm text-emerald-700">{note}</p>}
      </section>
    </main>
  )
}
