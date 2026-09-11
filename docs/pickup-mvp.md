# Restaurant pickup MVP

The initial audience is restaurants and local food businesses. Customers collect
orders at the business; there are no delivery addresses, drivers, or delivery fees.

## Implemented domain foundation

- Restaurant ownership references an account identifier; existing restaurants may
  remain unassigned but cannot accept orders until an owner is assigned.
- Menu items belong to one restaurant, with availability and USD prices.
- Pickup orders snapshot item names and prices and calculate an item subtotal.
- Orders require an open, active restaurant, a customer identifier, and available
  items from that restaurant with positive quantities.
- Status flow: Pending → Accepted → Preparing → ReadyForPickup → PickedUp.
- Pending orders may be rejected or cancelled. Accepted orders may be cancelled.
  Preparing orders cannot be cancelled in this initial domain policy.

## Next implementation stages

1. Implemented: PostgreSQL mappings/migration for accounts, restaurants and menu
   items; ASP.NET Identity registration/login; authenticated restaurant ownership
   and profile/availability management. See [local setup](local-development.md).
   Local PostgreSQL signup/login and restaurant persistence were verified.
   Email verification delivery remains pending.
2. Implemented: authorized menu management endpoints and public restaurant
   browsing with search, city/open-status filters and pagination. Menu writes
   verify authenticated ownership and the item's restaurant. Unavailable items
   remain visible as sold out; deleted items and inactive/deleted/unowned
   restaurants are excluded from public browsing.
3. Order endpoints that load current menu items from storage, calculate prices
   on the server, enforce customer/owner permissions, and handle concurrent status
   updates and duplicate submissions.
4. Customer browsing, cart and order tracking; business onboarding and an order
   dashboard in React.
5. Decide pay-at-pickup versus online payment, then implement taxes, final totals,
   payment handling where applicable, and notifications.

Account identifiers in the domain are references. Authentication and restaurant
ownership checks now happen in the API/service layers. Order persistence and
ordering endpoints are not implemented yet.
Subtotal excludes taxes, tips, and fees and is not a final payment amount.
