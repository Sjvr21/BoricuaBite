import { FormEvent, useEffect, useState } from 'react'
import { api } from '../api'
import type { Order } from '../types'

export function OrdersPage({ run }: { run: (action: () => Promise<void>) => Promise<void> }) {
  const [orders, setOrders] = useState<Order[]>([])
  const [reviewing, setReviewing] = useState<string | null>(null)
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [reviewed, setReviewed] = useState<Set<string>>(new Set())

  async function load() {
    setOrders(await api.customerOrders())
  }

  useEffect(() => { void run(load) }, [])

  async function submitReview(event: FormEvent, order: Order) {
    event.preventDefault()
    await run(async () => {
      await api.createReview(order.id, rating, comment)
      setReviewed(current => new Set(current).add(order.id))
      setReviewing(null)
      setRating(5)
      setComment('')
    })
  }

  return (
    <main className="mx-auto max-w-5xl px-5 py-10">
      <div className="mb-8">
        <p className="text-sm font-black uppercase tracking-[0.2em] text-orange-600">Customer account</p>
        <h1 className="text-4xl font-black">My Orders</h1>
        <p className="mt-2 text-stone-600">Track pickup progress, payment status, totals, taxes, and fees.</p>
      </div>

      {orders.length === 0 ? (
        <section className="rounded-3xl border border-dashed border-stone-300 bg-white p-10 text-center text-stone-500">You have not placed an order yet.</section>
      ) : (
        <div className="space-y-5">
          {orders.map(order => (
            <article key={order.id} className="rounded-3xl border border-stone-200 bg-white p-6 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <p className="text-sm font-bold text-orange-600">{order.restaurantName}</p>
                  <h2 className="text-2xl font-black">Order #{order.id.slice(0, 8)}</h2>
                  <p className="mt-1 text-sm text-stone-500">{new Date(order.createdAtUtc).toLocaleString()}</p>
                </div>
                <div className="text-right">
                  <span className="rounded-full bg-stone-100 px-3 py-1 text-sm font-black">{prettyStatus(order.status)}</span>
                  <p className="mt-2 text-sm font-semibold text-stone-500">{order.paymentMethod === 'Online' ? 'Online payment' : 'Pay at restaurant'} · {order.paymentStatus}</p>
                </div>
              </div>

              <div className="mt-5 space-y-2 rounded-2xl bg-stone-50 p-4">
                {order.items.map(item => (
                  <div key={item.menuItemId} className="flex justify-between text-sm"><span>{item.quantity} × {item.name}</span><span className="font-semibold">${item.subtotal.toFixed(2)}</span></div>
                ))}
                <div className="mt-3 border-t border-stone-200 pt-3 text-sm">
                  <MoneyRow label="Food subtotal" value={order.subtotal} />
                  <MoneyRow label="Tax" value={order.taxAmount} />
                  <MoneyRow label="BoricuaBite service fee" value={order.serviceFee} />
                  <div className="mt-2 flex justify-between text-base font-black"><span>Total</span><span>${order.total.toFixed(2)}</span></div>
                </div>
              </div>

              {order.status === 'Completed' && !reviewed.has(order.id) && (
                reviewing === order.id ? (
                  <form onSubmit={event => void submitReview(event, order)} className="mt-5 rounded-2xl border border-amber-200 bg-amber-50 p-4">
                    <h3 className="font-black">Review {order.restaurantName}</h3>
                    <div className="mt-3 flex gap-2">{[1, 2, 3, 4, 5].map(value => <button key={value} type="button" onClick={() => setRating(value)} className={`text-2xl ${value <= rating ? 'text-amber-500' : 'text-stone-300'}`}>★</button>)}</div>
                    <textarea value={comment} onChange={event => setComment(event.target.value)} maxLength={2000} placeholder="Tell other customers about your experience" className="mt-3 min-h-24 w-full rounded-xl border border-stone-200 bg-white p-3" />
                    <div className="mt-3 flex gap-2"><button className="rounded-xl bg-stone-900 px-4 py-2 text-sm font-bold text-white">Submit verified review</button><button type="button" onClick={() => setReviewing(null)} className="rounded-xl px-4 py-2 text-sm font-semibold">Cancel</button></div>
                  </form>
                ) : (
                  <button onClick={() => setReviewing(order.id)} className="mt-5 rounded-xl bg-amber-50 px-4 py-2 text-sm font-bold text-amber-800">Leave verified review</button>
                )
              )}
              {reviewed.has(order.id) && <p className="mt-5 text-sm font-bold text-emerald-700">Review submitted.</p>}
            </article>
          ))}
        </div>
      )}
    </main>
  )
}

function MoneyRow({ label, value }: { label: string; value: number }) {
  return <div className="flex justify-between py-0.5"><span className="text-stone-500">{label}</span><span>${value.toFixed(2)}</span></div>
}

function prettyStatus(status: Order['status']) {
  return status === 'ReadyForPickup' ? 'Ready for pickup' : status
}
