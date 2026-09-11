# Run accounts and restaurant onboarding locally

## Prerequisites

- The .NET 9 SDK used by the existing solution.
- PostgreSQL 16, or Docker with Compose to run the provided database container.

Run commands from the repository root. The API requires a connection string;
credentials are not stored in source control.

## Start PostgreSQL and configure the API

For the included local container, choose a local password and run:

```sh
export POSTGRES_PASSWORD='replace-with-your-local-password'
docker compose up -d postgres
```

Configure the matching connection string with .NET user secrets:

```sh
dotnet user-secrets set 'ConnectionStrings:BoricuaBite' \
  "Host=localhost;Port=5432;Database=boricuabite;Username=boricuabite;Password=$POSTGRES_PASSWORD" \
  --project backend/BoricuaBite.Api
```

An existing PostgreSQL instance also works: set the secret to its connection
string instead. Deployments can supply `ConnectionStrings__BoricuaBite` through
their secret manager. Do not commit connection strings with passwords.

## Apply the migration and start the API

```sh
dotnet restore backend/BoricuaBite.sln
dotnet tool restore
dotnet ef database update --project backend/BoricuaBite.Infrastructure \
  --startup-project backend/BoricuaBite.Api -- --environment Development
dotnet dev-certs https --trust
dotnet run --project backend/BoricuaBite.Api --launch-profile https
```

The API listens at `https://localhost:7144`. Development OpenAPI is at
`https://localhost:7144/openapi/v1.json`. Migrations are explicitly applied;
starting the API does not modify the database schema automatically.

## Account and owner flow

Use the requests in `backend/BoricuaBite.Api/BoricuaBite.Api.http`, or any HTTP client.

1. `POST /api/auth/register` with an email and a password of at least 12 characters
   including uppercase, lowercase, a digit, and a symbol. Success is HTTP 200.
2. `POST /api/auth/login?useCookies=false` with the same credentials. Save the
   `accessToken` and `refreshToken` response fields securely.
3. Send `Authorization: Bearer <accessToken>` for account and owner requests.
4. `GET /api/account` returns the signed-in account identifier and email.
5. `POST /api/owner/restaurants` creates a restaurant linked to this account.
   Its response includes the restaurant ID and a `Location` header.
6. `GET /api/owner/restaurants` lists this account's restaurants. Use
   `GET /api/owner/restaurants/{id}` to read one restaurant.
7. `PUT /api/owner/restaurants/{id}` replaces its editable profile, including
   pickup address. Use the same body as creation.
8. `PUT /api/owner/restaurants/{id}/availability` with `{"isOpen":true}` or
   `{"isOpen":false}` controls whether the business is open. New restaurants
   start closed. This flag will be used by the future order-placement API.
9. `POST /api/auth/refresh` with `{"refreshToken":"..."}` renews the tokens.

One account may be a customer and own restaurants. Ownership is assigned from
authentication, never from request JSON. Other users receive HTTP 404 when
requesting or changing an owner's restaurant. Anonymous requests receive HTTP 401.

Example restaurant body:

```json
{
  "name": "Local Kitchen",
  "description": "Puerto Rican food for pickup",
  "phoneNumber": "787-555-0123",
  "address": {
    "addressLine1": "123 Main Street",
    "addressLine2": null,
    "city": "Vega Baja",
    "stateOrTerritory": "Puerto Rico",
    "postalCode": "00693",
    "country": "US"
  }
}
```

## Tests

```sh
dotnet test backend/BoricuaBite.sln
```

API integration tests use an isolated relational SQLite database and the real
Identity authentication pipeline. They require no database credentials. They
cover password hashing, duplicate registration, lockout, login/refresh,
validation, persistence, ownership isolation, soft deletion, and throttling.
PostgreSQL migration/model checks generate PostgreSQL SQL without a server.
A local PostgreSQL 16 smoke test also verified migration application, signup,
login, restaurant creation/reload, and rejection of incorrect database passwords.
Its temporary account and restaurant were removed afterward. Repeat this check
when configuring another environment.

For a Homebrew PostgreSQL installation, start the database with
`brew services start postgresql@16`. After the database and user secrets have been
configured, restart the API with the `dotnet run` command above. Keep the app's
database login restricted to its own database and require password authentication
for that login in `pg_hba.conf`; a fresh Homebrew cluster may use `trust` locally.

## Current boundaries

See [menu management and browsing](menu-and-browsing.md) for the new endpoints,
filters, pagination and visibility rules. The HTTP request file includes examples.

- These are backend endpoints; frontend signup and business dashboard screens
  are still to be built. Menu management and public restaurant/menu browsing
  endpoints are available. Pickup orders remain domain models, without persistence
  or endpoints.
- Identity issues protected opaque bearer tokens, **not JWTs**. Access tokens
  last 15 minutes; refresh tokens last 7 days. See the authentication decision.
  Owner and account endpoints require bearer authentication even if a caller
  requests a cookie from the built-in login endpoint.
- Email delivery is not configured. Identity maps confirmation and password
  recovery endpoints, but their emails are not delivered yet, and verified email
  is not required for this development milestone. Configure an email sender and
  confirmation requirements before a public signup launch.
- Authentication is limited to 20 requests per minute per remote IP per API
  instance; login locks an account for 15 minutes after five failed attempts.
  Configure trusted proxies before relying on client IP limits behind a proxy.
- Persist and protect ASP.NET Data Protection keys in deployment so tokens survive
  restarts and work across replicas. Browser token/session handling is part of
  the upcoming frontend work.
- Upgrade the existing .NET 9 target/runtime to a supported release before
  production deployment; that platform migration is outside this milestone.
