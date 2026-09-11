# ADR-003: Identity accounts and restaurant ownership

## Status

Accepted for the initial backend milestone.

## Decision

Use ASP.NET Core Identity API endpoints with EF Core and PostgreSQL. Account IDs
are GUIDs, matching the existing domain ownership identifiers. A single account
can order as a customer and own multiple restaurants. Owner capabilities are
checked against each restaurant's stored owner, rather than a client-supplied
role or owner ID. There is no public administrator role assignment endpoint.

Use Identity's built-in protected bearer tokens for the current account and owner
APIs. This replaces the README's planned JWT implementation for this milestone.
The tokens are opaque, not JWTs, and should not be decoded by clients. Identity
handles password hashing, lockout, and token generation and renewal.

## Consequences

The initial client uses `/api/auth/login?useCookies=false` and an Authorization
header. Account and restaurant endpoints explicitly require the bearer scheme.
Cookie login is exposed by Identity but does not grant access to these endpoints.
The future browser client must choose secure session storage or a cookie-based
session architecture with CSRF protection; do not store long-lived tokens in
local storage by default.

Data Protection key persistence and sharing are required for deployment. If
third-party clients later need standard OAuth/OIDC or JWT interoperability,
integrate a suitable identity provider rather than inventing a token protocol.

Email confirmation and recovery delivery still need an email provider. The
current development flow permits signup and login without confirmation.

## References

- [Microsoft: Identity API authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-9.0)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
