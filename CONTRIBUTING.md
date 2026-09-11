# Contributing to BoricuaBite

BoricuaBite follows a feature-branch workflow with pull requests and code review.

Follow [the security requirements](SECURITY.md) for every change. Real database
credentials, authentication tokens, database backups and customer data must never
be committed. Use user secrets locally and deployment secret storage in production.

## Branches

- `main` contains stable releases.
- `develop` contains integrated development work.
- Feature branches are created from `develop`.

Examples:

```text
feature/address-value-object
feature/restaurant-hours
fix/order-total-calculation
docs/update-architecture
