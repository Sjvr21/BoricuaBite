# BoricuaBite Roadmap

## Phase 1: Engineering Foundation

- [x] Create repository
- [x] Create backend solution structure
- [x] Create Domain base entity
- [x] Create Restaurant entity
- [ ] Add repository documentation
- [ ] Add automated build workflow
- [x] Add testing projects

## Phase 2: Domain Model

- [x] Address value object
- [x] Restaurant ownership
- [ ] Business hours
- [ ] Restaurant images
- [ ] Menu categories
- [x] Menu items
- [ ] Customization options
- [x] Orders and order items
- [ ] Payments
- [ ] Reviews
- [ ] Favorites

## Phase 3: Persistence

- [x] PostgreSQL provider configuration and local live database verification
- [x] Entity Framework Core
- [x] Account, restaurant and menu item configurations
- [x] Initial accounts and restaurants migration
- [ ] Order persistence and configuration
- [ ] Seed data
- [ ] Integration testing database

## Phase 4: Authentication and Authorization

- [x] ASP.NET Core Identity
- [x] Identity bearer authentication (opaque tokens; see ADR-003)
- [ ] Role authorization
- [ ] Email confirmation
- [ ] Password reset
- [ ] Google OAuth
- [ ] Email-based 2FA

## Phase 5: Restaurant Management

- [ ] Admin restaurant management
- [x] Authenticated owner account assignment
- [x] Owner restaurant profile API
- [x] Menu item management (create, edit, availability, soft delete)
- [x] Restaurant open/closed availability management
- [ ] Business hours management

## Phase 6: Customer Ordering

- [x] Restaurant browsing and search
- [ ] Favorites
- [ ] Cart
- [ ] Order placement
- [ ] Order status tracking
- [ ] Reviews

## Phase 7: Payments and Notifications

- [ ] Stripe Checkout
- [ ] Stripe Connect
- [ ] Payment webhooks
- [ ] Email receipts
- [ ] Order status notifications
- [ ] Background jobs

## Phase 8: Frontend

- [ ] React and TypeScript setup
- [ ] Customer application
- [ ] Owner dashboard
- [ ] Admin dashboard
- [ ] Responsive design
- [ ] Accessibility review

## Phase 9: Production Engineering

- [ ] Docker
- [ ] Redis
- [ ] Rate limiting
- [ ] API versioning
- [ ] Structured logging
- [ ] Monitoring
- [ ] GitHub Actions
- [ ] Production deployment
