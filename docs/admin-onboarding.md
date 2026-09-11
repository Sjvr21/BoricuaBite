# Admin restaurant onboarding

BoricuaBite uses a platform-managed restaurant model:

- Anyone may create an account.
- Regular accounts cannot create restaurant listings.
- The configured platform admin creates a restaurant and assigns it to an existing account by email.
- Assigned owners can manage their restaurant availability and menu.
- The admin can suspend or reactivate a restaurant. Suspended restaurants are hidden from the public catalog and forced closed.

## Configure the local admin

Use .NET user secrets so the admin email is not committed:

```sh
dotnet user-secrets set 'Admin:Email' 'your-admin-email@example.com' \
  --project backend/BoricuaBite.Api
```

The email must match an existing BoricuaBite account. Restart the API after changing the setting.

## Development flow

1. Create the admin account through the normal registration screen if it does not exist.
2. Configure `Admin:Email` to that account email and restart the API.
3. A restaurant owner creates their own BoricuaBite account.
4. Sign in as the admin and open **Admin Dashboard**.
5. Enter the owner's account email and the restaurant details.
6. The new restaurant is assigned to that owner and becomes visible in their Owner Dashboard.
7. Use **Suspend** / **Activate** in Admin Dashboard to control whether the listing is publicly published.

## Billing boundary

This milestone does not charge cards yet. `Restaurant.IsActive` is the publication/access switch that a later Stripe subscription webhook can control. A future billing integration should activate a listing after successful onboarding/payment and suspend it when the paid service is cancelled or delinquent according to the product's billing rules.
