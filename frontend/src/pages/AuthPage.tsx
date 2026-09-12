import { FormEvent, useEffect, useRef, useState } from 'react'
import { api } from '../api'
import type { AuthResponse } from '../types'

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

type Mode = 'login' | 'register' | 'forgot' | 'reset'

export function AuthPage({
  onAuthenticated,
  loading,
  run,
}: {
  onAuthenticated: (auth: AuthResponse) => Promise<void>
  loading: boolean
  run: (action: () => Promise<void>) => Promise<void>
}) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [mode, setMode] = useState<Mode>('login')
  const [note, setNote] = useState('')
  const [challengeId, setChallengeId] = useState('')
  const [code, setCode] = useState('')
  const [resetCode, setResetCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const googleButton = useRef<HTMLDivElement>(null)
  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

  useEffect(() => {
    if (!googleClientId || !googleButton.current || mode !== 'login' || challengeId) return

    const render = () => {
      if (!window.google || !googleButton.current) return
      window.google.accounts.id.initialize({
        client_id: googleClientId,
        callback: response => {
          void run(async () => {
            const auth = await api.googleSignIn(response.credential)
            await onAuthenticated(auth)
          })
        },
      })
      googleButton.current.innerHTML = ''
      window.google.accounts.id.renderButton(googleButton.current, {
        theme: 'outline',
        size: 'large',
        shape: 'pill',
        text: 'continue_with',
        width: 360,
      })
    }

    if (window.google) {
      render()
      return
    }

    const script = document.createElement('script')
    script.src = 'https://accounts.google.com/gsi/client'
    script.async = true
    script.defer = true
    script.onload = render
    document.head.appendChild(script)
    return () => script.remove()
  }, [googleClientId, onAuthenticated, run, mode, challengeId])

  async function submit(event: FormEvent) {
    event.preventDefault()

    if (mode === 'register') {
      await run(async () => {
        await api.register(email, password)
        const challenge = await api.startPasswordLogin(email, password)
        setMode('login')
        setChallengeId(challenge.challengeId)
        setNote(`Account created. We sent a 6-digit code to ${challenge.destination}.${challenge.developmentCode ? ` Development code: ${challenge.developmentCode}` : ''}`)
      })
      return
    }

    if (mode === 'forgot') {
      await run(async () => {
        const result = await api.forgotPassword(email)
        setMode('reset')
        setNote(`${result.message}${result.developmentResetCode ? ` Development reset code: ${result.developmentResetCode}` : ''}`)
      })
      return
    }

    if (mode === 'reset') {
      await run(async () => {
        await api.resetPassword(email, resetCode, newPassword)
        setPassword('')
        setNewPassword('')
        setResetCode('')
        setMode('login')
        setNote('Password reset successfully. Sign in with your new password.')
      })
      return
    }

    if (!challengeId) {
      await run(async () => {
        const challenge = await api.startPasswordLogin(email, password)
        setChallengeId(challenge.challengeId)
        setNote(`We sent a 6-digit code to ${challenge.destination}.${challenge.developmentCode ? ` Development code: ${challenge.developmentCode}` : ''}`)
      })
      return
    }

    await run(async () => {
      const auth = await api.verifyPasswordLogin(challengeId, code)
      await onAuthenticated(auth)
    })
  }

  function resetChallenge() {
    setChallengeId('')
    setCode('')
    setNote('')
  }

  function switchMode(next: Mode) {
    resetChallenge()
    setResetCode('')
    setNewPassword('')
    setMode(next)
  }

  return (
    <main className="mx-auto grid min-h-[75vh] max-w-5xl items-center gap-10 px-5 py-12 md:grid-cols-2">
      <section className="min-w-0">
        <p className="text-sm font-black uppercase tracking-[0.2em] text-orange-600">Secure BoricuaBite account</p>
        <h1 className="mt-2 text-5xl font-black leading-tight">Order local food or manage your business.</h1>
        <p className="mt-5 text-lg text-stone-600">
          Password sign-in uses an emailed verification code. You can also continue with Google when Google sign-in is configured.
        </p>
      </section>

      <div className="min-w-0 max-w-full overflow-hidden rounded-3xl border border-stone-200 bg-white p-7 shadow-xl">
        {!challengeId && mode !== 'reset' && mode !== 'forgot' && (
          <div className="mb-6 flex rounded-2xl bg-stone-100 p-1">
            <button type="button" onClick={() => switchMode('login')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'login' ? 'bg-white shadow-sm' : ''}`}>Sign in</button>
            <button type="button" onClick={() => switchMode('register')} className={`flex-1 rounded-xl py-2 text-sm font-bold ${mode === 'register' ? 'bg-white shadow-sm' : ''}`}>Create account</button>
          </div>
        )}

        {(mode === 'forgot' || mode === 'reset') && (
          <div className="mb-5 min-w-0">
            <button type="button" onClick={() => switchMode('login')} className="text-sm font-semibold text-stone-500 hover:text-stone-900">← Back to sign in</button>
            <h2 className="mt-3 text-2xl font-black">{mode === 'forgot' ? 'Reset your password' : 'Choose a new password'}</h2>
          </div>
        )}

        <form onSubmit={submit} className="min-w-0 max-w-full">
          {mode === 'forgot' ? (
            <>
              <label className="mb-2 block text-sm font-bold">Email</label>
              <input type="email" required value={email} onChange={event => setEmail(event.target.value)} className="w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
              <p className="mt-2 text-xs text-stone-500">We’ll email you a reset code if an account exists for this address.</p>
            </>
          ) : mode === 'reset' ? (
            <>
              <label className="mb-2 block text-sm font-bold">Email</label>
              <input type="email" required value={email} onChange={event => setEmail(event.target.value)} className="mb-4 w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />

              <label className="mb-2 block text-sm font-bold">Reset code</label>
              <input
                type="text"
                required
                autoComplete="one-time-code"
                value={resetCode}
                onChange={event => setResetCode(event.target.value.trim())}
                placeholder="Paste reset code"
                className="mb-2 block w-full min-w-0 max-w-full overflow-hidden text-ellipsis whitespace-nowrap rounded-2xl border border-stone-200 px-4 py-3 font-mono text-xs outline-none focus:border-orange-400"
              />
              <p className="mb-4 text-xs text-stone-500">Paste the full code from the email. Long codes stay contained inside this field.</p>

              <label className="mb-2 block text-sm font-bold">New password</label>
              <input type="password" required minLength={12} value={newPassword} onChange={event => setNewPassword(event.target.value)} className="w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
              <p className="mt-2 text-xs text-stone-500">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>
            </>
          ) : !challengeId ? (
            <>
              <label className="mb-2 block text-sm font-bold">Email</label>
              <input type="email" required value={email} onChange={event => setEmail(event.target.value)} className="mb-4 w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />

              <label className="mb-2 block text-sm font-bold">Password</label>
              <input type="password" required minLength={12} value={password} onChange={event => setPassword(event.target.value)} className="w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 outline-none focus:border-orange-400" />
              {mode === 'register' && <p className="mt-2 text-xs text-stone-500">Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</p>}
              {mode === 'login' && (
                <button type="button" onClick={() => switchMode('forgot')} className="mt-3 text-sm font-semibold text-orange-700 hover:text-orange-600">Forgot password?</button>
              )}
            </>
          ) : (
            <>
              <p className="mb-4 text-sm font-semibold text-stone-600">Second step: enter the code sent to your email.</p>
              <label className="mb-2 block text-sm font-bold">6-digit verification code</label>
              <input inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required value={code} onChange={event => setCode(event.target.value.replace(/\D/g, '').slice(0, 6))} className="w-full min-w-0 max-w-full rounded-2xl border border-stone-200 px-4 py-3 text-center text-2xl font-black tracking-[0.4em] outline-none focus:border-orange-400" />
              <button type="button" onClick={resetChallenge} className="mt-3 text-sm font-semibold text-stone-500 hover:text-stone-900">Use a different email/password</button>
            </>
          )}

          {note && <p className="mt-3 max-w-full break-all text-sm text-emerald-700">{note}</p>}

          <button disabled={loading} className="mt-6 w-full rounded-2xl bg-orange-600 px-5 py-3 font-black text-white hover:bg-orange-500 disabled:opacity-60">
            {loading
              ? 'Working...'
              : mode === 'register'
                ? 'Create account'
                : mode === 'forgot'
                  ? 'Send reset code'
                  : mode === 'reset'
                    ? 'Reset password'
                    : challengeId
                      ? 'Verify & sign in'
                      : 'Continue'}
          </button>
        </form>

        {mode === 'login' && !challengeId && (
          <>
            <div className="my-5 flex items-center gap-3 text-xs font-bold uppercase tracking-widest text-stone-400">
              <div className="h-px flex-1 bg-stone-200" />or<div className="h-px flex-1 bg-stone-200" />
            </div>
            {googleClientId ? (
              <div ref={googleButton} className="flex min-h-11 justify-center" />
            ) : (
              <div className="rounded-2xl border border-dashed border-stone-300 bg-stone-50 px-4 py-3 text-center">
                <div className="font-semibold text-stone-700">Continue with Google</div>
                <div className="mt-1 text-xs text-stone-500">Google sign-in is not configured yet. Add VITE_GOOGLE_CLIENT_ID and restart Vite.</div>
              </div>
            )}
          </>
        )}
      </div>
    </main>
  )
}
