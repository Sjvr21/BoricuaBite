# BoricuaBite

A full-stack restaurant marketplace built with ASP.NET Core and React.

## Project Goals

This project is being developed as a real-world portfolio application following professional software engineering practices.

Features include:

- Restaurant management
- Customer accounts
- Google Authentication
- Online ordering
- Stripe payments
- Email verification
- Reviews
- Admin dashboard
- Restaurant owner dashboard

## Tech Stack

Backend
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL

Frontend
- React
- TypeScript
- Tailwind CSS

Authentication
- ASP.NET Identity
- Identity bearer tokens (see [authentication decision](docs/decisions/ADR-003-account-authentication.md))
- Google OAuth

Payments
- Stripe Connect

Email
- SendGrid

## Project Status

🚧 Currently under development.

Account registration, login, token refresh, and owner-scoped restaurant setup
are implemented in the backend, with PostgreSQL persistence and migrations.
The React frontend and customer ordering API are still planned.

See [local setup and API usage](docs/local-development.md) to run this milestone.
