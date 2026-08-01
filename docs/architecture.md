# BoricuaBite Architecture

BoricuaBite is a multi-tenant restaurant ordering platform built with ASP.NET Core and React.

## Backend Projects

### Domain

Contains entities, value objects, enums, domain rules, and domain events.

The Domain project must remain independent from databases, web frameworks, payment providers, and email providers.

### Application

Contains use cases, DTOs, validation, interfaces, commands, queries, and application services.

It coordinates domain behavior without depending on specific infrastructure implementations.

### Infrastructure

Contains implementations for:

- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- Email delivery
- Stripe payments
- File storage
- Redis
- Background jobs
- Logging and monitoring integrations

### API

Contains:

- Controllers or API endpoints
- Middleware
- Authentication configuration
- Dependency injection
- OpenAPI configuration
- Rate limiting
- Application startup

## Dependency Direction

```text
API ───────────────► Application
 │                         │
 └────► Infrastructure     ▼
               │         Domain
               └──────────►