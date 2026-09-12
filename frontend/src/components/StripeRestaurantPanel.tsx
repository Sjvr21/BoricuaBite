import { useEffect, useState } from 'react'
import { loadConnectAndInitialize } from '@stripe/connect-js'
import {
  ConnectAccountOnboarding,
  ConnectComponentsProvider,
  ConnectNotificationBanner,
} from '@stripe/react-connect-js'
import { api } from '../api'
import type { Restaurant, RestaurantStripeStatus } from '../types'

type ConnectInstance = ReturnType<typeof loadConnectAndInitialize>

export function StripeRestaurantPanel({
  restaurant,
  loading,
  run,
  onSubscriptionChanged,
}: {
  restaurant: Restaurant
  loading: boolean
  run: (action: () => Promise<void>) => Promise<void>
  onSubscriptionChanged: () => Promise<void>
}) {
  const [status, setStatus] = useState<RestaurantStripeStatus | null>(null)
  const [connectInstance, setConnectInstance] = useState<ConnectInstance | null>(null)
  const [showOnboarding, setShowOnboarding] = useState(false)

  async function refreshStatus() {
    setStatus(await api.restaurantStripeStatus(restaurant.id))
  }

  useEffect(() => {
    setConnectInstance(null)
    setShowOnboarding(false)
    void run(refreshStatus)
  }, [restaurant.id])

  async function beginOnboarding() {
    await run(async () => {
      const bootstrap = await api.createStripeConnectSession(restaurant.id)
      let initialSecret: string | null = bootstrap.clientSecret
      const instance = loadConnectAndInitialize({
        publishableKey: bootstrap.publishableKey,
        fetchClientSecret: async () => {
          if (initialSecret) {
            const secret = initialSecret
            initialSecret = null
            return secret
          }
          const next = await api.createStripeConnectSession(restaurant.id)
          return next.clientSecret
        },
      })
      setStatus(bootstrap)
      setConnectInstance(instance)
      setShowOnboarding(true)
    })
  }

  async function openExpressDashboard() {
    await run(async () => {
      const result = await api.createStripeExpressDashboard(restaurant.id)
      window.location.assign(result.url)
    })
  }

  async function startSubscription() {
    await run(async () => {
      const result = await api.createSubscriptionCheckout(restaurant.id)
      window.location.assign(result.checkoutUrl)
    })
  }

  async function manageSubscription() {
    await run(async () => {
      const result = await api.createSubscriptionPortal(restaurant.id)
      window.location.assign(result.portalUrl)
    })
  }

  const canStartSubscription = status?.isReady === true &&
    (restaurant.subscriptionStatus === 'Pending' || restaurant.subscriptionStatus === 'Cancelled')
  const canManageSubscription = restaurant.subscriptionStatus === 'Active' || restaurant.subscriptionStatus === 'PastDue'

  return (
    <section className="rounded-3xl border border-stone-200 bg-white p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="text-sm font-bold text-orange-600">Stripe</p>
          <h2 className="text-2xl font-black">Payouts & membership</h2>
          <p className="mt-1 max-w-2xl text-sm text-stone-500">
            Verify the restaurant and bank account with Stripe, then start the $19.99/month membership with a 30-day free trial.
          </p>
        </div>
        <button
          type="button"
          disabled={loading}
          onClick={() => void run(refreshStatus)}
          className="rounded-xl bg-stone-100 px-3 py-2 text-sm font-bold disabled:opacity-50"
        >
          Refresh status
        </button>
      </div>

      <div className="mt-5 grid gap-3 sm:grid-cols-3">
        <StatusCard label="Stripe account" value={status?.connectedAccountId ? 'Created' : 'Not started'} ready={Boolean(status?.connectedAccountId)} />
        <StatusCard label="Transfers" value={status?.transfersActive ? 'Active' : 'Needs setup'} ready={status?.transfersActive === true} />
        <StatusCard label="Payouts" value={status?.payoutsActive ? 'Active' : 'Needs setup'} ready={status?.payoutsActive === true} />
      </div>

      <div className="mt-5 flex flex-wrap gap-3">
        <button
          type="button"
          disabled={loading}
          onClick={() => void beginOnboarding()}
          className="rounded-2xl bg-violet-600 px-5 py-3 font-black text-white disabled:bg-stone-300"
        >
          {status?.connectedAccountId ? 'Continue Stripe setup' : 'Complete Stripe setup'}
        </button>

        {status?.connectedAccountId && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void openExpressDashboard()}
            className="rounded-2xl border border-stone-300 px-5 py-3 font-black text-stone-700 disabled:opacity-50"
          >
            Open Stripe dashboard
          </button>
        )}

        {canStartSubscription && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void startSubscription()}
            className="rounded-2xl bg-orange-600 px-5 py-3 font-black text-white disabled:bg-stone-300"
          >
            Start 30-day free trial
          </button>
        )}

        {canManageSubscription && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void manageSubscription()}
            className="rounded-2xl bg-stone-900 px-5 py-3 font-black text-white disabled:bg-stone-300"
          >
            Manage billing
          </button>
        )}
      </div>

      {restaurant.subscriptionStatus === 'PastDue' && (
        <p className="mt-4 rounded-xl bg-amber-50 p-3 text-sm font-semibold text-amber-800">
          Your latest membership payment failed. Stripe is retrying it; update the payment method in Manage billing to avoid suspension.
        </p>
      )}

      {showOnboarding && connectInstance && (
        <div className="mt-6 overflow-hidden rounded-2xl border border-stone-200 p-4">
          <ConnectComponentsProvider connectInstance={connectInstance}>
            <ConnectNotificationBanner />
            <ConnectAccountOnboarding
              onExit={() => {
                setShowOnboarding(false)
                void run(async () => {
                  await refreshStatus()
                  await onSubscriptionChanged()
                })
              }}
            />
          </ConnectComponentsProvider>
        </div>
      )}
    </section>
  )
}

function StatusCard({ label, value, ready }: { label: string; value: string; ready: boolean }) {
  return (
    <div className={`rounded-2xl p-4 ${ready ? 'bg-emerald-50 text-emerald-800' : 'bg-stone-50 text-stone-600'}`}>
      <p className="text-xs font-bold uppercase tracking-wide">{label}</p>
      <p className="mt-1 font-black">{value}</p>
    </div>
  )
}
