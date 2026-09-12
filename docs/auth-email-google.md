# Authentication and transactional email setup

This branch adds three production-oriented capabilities to BoricuaBite:

- password sign-in with email two-factor authentication (2FA)
- Google Identity Services sign-in and account linking
- SendGrid transactional email for sign-in codes and customer order receipts/status notifications

No API keys or OAuth secrets belong in the repository.

## 1. SendGrid transactional email

Create a SendGrid account, authenticate the sender/domain you want BoricuaBite to send from, and create an API key with the minimum mail-send permission needed by the application.

Store values through .NET user-secrets for local development:

```bash
dotnet user-secrets set 'Email:ApiKey' 'YOUR_SENDGRID_API_KEY' --project backend/BoricuaBite.Api
dotnet user-secrets set 'Email:FromEmail' 'orders@YOUR_DOMAIN' --project backend/BoricuaBite.Api
dotnet user-secrets set 'Email:FromName' 'BoricuaBite' --project backend/BoricuaBite.Api
```

The API never exposes the email API key to React.

When email is configured:

- password login sends a six-digit one-time code
- the code expires after 10 minutes
- five incorrect attempts invalidate the challenge
- previous active login challenges are consumed when a new one is requested
- pay-at-store customers receive an order confirmation receipt
- online-payment customers receive a paid receipt only after Stripe webhook confirmation
- customers receive status emails as restaurant owners move the order through the workflow

Email failures are logged but do not roll back an already-created or already-paid order.

When SendGrid is not configured, Development and Testing environments return the temporary code in the API response so local/CI testing remains possible. Production refuses password sign-in if email delivery is unavailable.

## 2. Google Identity Services

In Google Cloud Console:

1. Configure the OAuth consent screen for BoricuaBite.
2. Create an OAuth 2.0 Client ID of type **Web application**.
3. Add the frontend origin used in development, for example `http://localhost:5173`, to Authorized JavaScript origins.
4. Add the eventual production BoricuaBite frontend origin before launch.

Configure the same Google Web Client ID in the backend:

```bash
dotnet user-secrets set 'GoogleAuth:ClientId' 'YOUR_GOOGLE_WEB_CLIENT_ID.apps.googleusercontent.com' --project backend/BoricuaBite.Api
```

Then create `frontend/.env.local` from `frontend/.env.example`:

```text
VITE_GOOGLE_CLIENT_ID=YOUR_GOOGLE_WEB_CLIENT_ID.apps.googleusercontent.com
```

Restart both the API and Vite after changing these values.

The browser sends Google's ID credential to the BoricuaBite API. The API validates the token audience/signature and requires a Google-verified email. BoricuaBite stores Google's stable external account identifier through ASP.NET Identity external-login storage. Google passwords are never sent to or stored by BoricuaBite.

### Existing BoricuaBite account

For takeover protection, a Google credential is **not automatically attached** to an existing BoricuaBite account simply because the email text matches.

An existing user should:

1. sign in using password + email 2FA
2. open **Security**
3. choose Google linking there
4. use the Google account with the same email address

After linking, Continue with Google can sign in directly.

### New Google user

If there is no BoricuaBite account with that email and no existing Google external login, Continue with Google creates the BoricuaBite account and links the verified Google identity.

## 3. Password login flow

The public password flow is:

```text
email + password
      ↓
password verified server-side
      ↓
6-digit email code
      ↓
challenge verified
      ↓
ASP.NET Identity bearer + refresh tokens
```

The normal password-only `/api/auth/login` route is blocked outside the isolated `Testing` environment so it cannot bypass BoricuaBite's email 2FA flow.

The 2FA challenge persisted in PostgreSQL stores a salted PBKDF2 hash rather than the plaintext code. Challenges have expiration, attempt count, and consumed state.

## 4. Database migration

After pulling the feature branch, apply the latest migration:

```bash
dotnet tool restore

dotnet ef database update \
  --project backend/BoricuaBite.Infrastructure \
  --startup-project backend/BoricuaBite.Api
```

The new authentication migration adds the durable login-challenge table. ASP.NET Identity's existing user-login table stores Google external login identifiers, so Google linking does not need a separate custom table.

## 5. Run locally

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

## 6. Production checklist

Before production:

- use a verified SendGrid sending domain, not an unverified personal sender
- keep SendGrid and Stripe keys only in the deployment secret manager
- configure the production Google JavaScript origin
- use the production Google client ID consistently on frontend and backend
- persist ASP.NET Data Protection keys when running multiple/restarted server instances
- monitor failed email sends and webhook failures
- retain rate limiting around authentication endpoints
- serve frontend/API over HTTPS
- test password 2FA, Google linking, Google sign-in, receipts, status emails, Stripe checkout, and refunds in staging
