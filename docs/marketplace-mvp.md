# BoricuaBite Marketplace MVP

BoricuaBite now supports three overlapping account experiences:

- **Customers** register, browse subscribed restaurants, build pickup orders, choose online payment or pay at the restaurant, track order status, and leave one verified review per completed order.
- **Restaurant owners** use a normal BoricuaBite account. The platform admin creates and assigns their restaurant after onboarding. Owners manage profile imagery, menu imagery, availability, and pickup orders.
- **Platform admin** onboards restaurant clients, controls listing suspension and subscription state, and records the restaurant's Stripe connected-account/subscription identifiers.

## Publication and subscription rules

A public restaurant must have all of the following:

- an owner account
- `IsActive = true`
- `IsPublished = true`
- `SubscriptionStatus = Active`

A restaurant may prepare its profile/menu while its subscription is pending, but it cannot open for marketplace orders until the subscription is active.

Set the platform admin email locally:

```bash
dotnet user-secrets set 'Admin:Email' 'YOUR_ADMIN_EMAIL' \
  --project backend/BoricuaBite.Api
```

## Pricing configuration

Marketplace monetary rates are configuration-driven. Development defaults are intentionally `0` so BoricuaBite never silently assumes a tax or fee percentage.

Configure values only after confirming the rates you intend to use:

```bash
dotnet user-secrets set 'MarketplacePricing:TaxRate' '0.00' \
  --project backend/BoricuaBite.Api

dotnet user-secrets set 'MarketplacePricing:CustomerServiceFeeRate' '0.00' \
  --project backend/BoricuaBite.Api

dotnet user-secrets set 'MarketplacePricing:CustomerServiceFeeFlat' '0.00' \
  --project backend/BoricuaBite.Api

dotnet user-secrets set 'MarketplacePricing:RestaurantCommissionRate' '0.00' \
  --project backend/BoricuaBite.Api
```

The server snapshots these amounts when an order is created:

- **Food subtotal**: current server-side menu prices × quantities
- **Tax amount**: subtotal × configured tax rate
- **Customer service fee**: subtotal × service-fee rate + flat service fee
- **Restaurant commission**: subtotal × configured commission rate
- **Customer total**: subtotal + tax + customer service fee
- **Estimated restaurant proceeds**: subtotal + tax - restaurant commission, before processor-specific fees/adjustments

The browser cannot submit its own totals.

## Stripe online order payments

Online payment is optional. Pay-at-restaurant orders work without Stripe.

Configure Stripe through user secrets/environment variables, never committed files:

```bash
dotnet user-secrets set 'Stripe:SecretKey' 'YOUR_STRIPE_TEST_SECRET_KEY' \
  --project backend/BoricuaBite.Api

dotnet user-secrets set 'Stripe:WebhookSecret' 'YOUR_STRIPE_WEBHOOK_SIGNING_SECRET' \
  --project backend/BoricuaBite.Api

dotnet user-secrets set 'Stripe:FrontendBaseUrl' 'http://localhost:5173' \
  --project backend/BoricuaBite.Api
```

Each restaurant also needs its Stripe Connect connected-account ID (`acct_...`) recorded from the Admin dashboard before customers can choose online payment for that restaurant.

Online orders use a Stripe Checkout destination charge. The configured BoricuaBite service fee + restaurant commission is sent as the platform application fee, while the remaining charge is transferred to the connected restaurant account. Payment is considered paid only after a valid signed Stripe webhook confirms it.

Webhook endpoint:

```text
POST /api/payments/stripe/webhook
```

For local Stripe CLI testing, forward Stripe events to the API webhook and store the resulting signing secret as `Stripe:WebhookSecret`.

A paid online order cannot be accepted before payment confirmation. If the restaurant rejects/cancels a paid online order, BoricuaBite issues a full Stripe refund and requests reversal of the destination transfer/application fee.

## Restaurant subscriptions

The current MVP follows the requested manual sales/onboarding model:

1. restaurant owner creates a normal BoricuaBite account
2. restaurant owner contacts BoricuaBite
3. admin verifies payment/subscription outside or through the chosen billing workflow
4. admin creates/assigns the restaurant
5. admin changes subscription state to `Active`
6. restaurant becomes eligible for the public catalog and ordering

The domain already stores `StripeSubscriptionId` and the subscription states `Pending`, `Active`, `PastDue`, and `Cancelled`. Automatic Stripe subscription checkout/webhook automation can be added later without changing the publication model.

## Orders

Order lifecycle:

```text
Pending -> Accepted -> Preparing -> ReadyForPickup -> Completed
   |          |
   +-> Rejected
              +-> Cancelled
```

Pay-at-restaurant orders become `Paid` when pickup is completed. Online orders become `Paid` only from the signed Stripe webhook.

## Verified reviews and ranking

A review is accepted only when:

- the order belongs to the logged-in customer
- the order is `Completed`
- the order has not already been reviewed

The default catalog ranking is:

1. highest verified average rating
2. highest verified review count
3. restaurant name

## Images

For the local MVP, owners can upload JPEG, PNG, or WebP images up to 5 MB. Files are stored under:

```text
backend/BoricuaBite.Api/wwwroot/uploads/restaurants/{restaurantId}/
```

Only the generated URL is stored in PostgreSQL. Replace this local storage path with object storage such as S3/Cloudinary before production or multi-instance deployment.

## Database migration

After pulling the marketplace branch, apply the generated EF migration:

```bash
dotnet tool restore

dotnet ef database update \
  --project backend/BoricuaBite.Infrastructure \
  --startup-project backend/BoricuaBite.Api \
  -- --environment Development
```

## Run locally

API:

```bash
dotnet run --project backend/BoricuaBite.Api --launch-profile https
```

Frontend:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`.

## Production follow-ups

Before processing real customer money:

- verify Puerto Rico tax collection/remittance responsibilities with appropriate accounting/legal guidance
- use Stripe test mode through the complete order/refund flow before live mode
- automate restaurant subscription billing/webhooks if desired
- move uploaded images to durable object storage
- add email/SMS order notifications
- add observability/audit logs for admin, payment, refund, and subscription actions
- add privacy/terms/refund policies and restaurant agreements
