# Menu management and customer browsing

These endpoints use the existing database migration; no schema changes are needed.
See [local setup](local-development.md) for login and starting the backend.

## Owner menu management

All routes below require `Authorization: Bearer <accessToken>`. The authenticated
account must own the restaurant. Supplying another restaurant's item ID returns
404, even when the account owns both restaurants. Restaurant ownership cannot be
changed through a menu request.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/owner/restaurants/{restaurantId}/menu-items` | List the menu |
| POST | `/api/owner/restaurants/{restaurantId}/menu-items` | Add a dish |
| GET | `/api/owner/restaurants/{restaurantId}/menu-items/{itemId}` | Read one dish |
| PUT | `/api/owner/restaurants/{restaurantId}/menu-items/{itemId}` | Edit its name and price |
| PUT | `/api/owner/restaurants/{restaurantId}/menu-items/{itemId}/availability` | Mark available or sold out |
| DELETE | `/api/owner/restaurants/{restaurantId}/menu-items/{itemId}` | Remove it from the menu |

Create/edit request:

```json
{"name":"Mofongo con pollo","price":14.50}
```

Names are required and limited to 150 characters. Prices are required, use USD,
must be nonnegative, have at most two decimal places, and fit the database's
numeric precision (maximum 9999999999.99). Missing prices are rejected instead of
silently becoming zero. New dishes are available by default.

Availability request:

```json
{"isAvailable":false}
```

Removal uses soft deletion: the item is retained in storage and hidden from both
public and owner menus. Existing order snapshots retain their original names and
prices when a menu item changes. Menu edits do not move items between restaurants.

## Public browsing

No login is required for these read-only routes. Public responses contain business
contact/pickup details, not owner account identifiers, email addresses, or credentials.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/restaurants` | Browse restaurants |
| GET | `/api/restaurants/{restaurantId}` | Read a restaurant profile |
| GET | `/api/restaurants/{restaurantId}/menu` | Read its menu |

Restaurant browsing accepts `search` (name/description substring, maximum 150
characters), `city` (exact city, maximum 100 characters), and `isOpen=true` or
`isOpen=false`. Search and city matching ignore case. Example:

```text
/api/restaurants?search=mofongo&city=Vega%20Baja&isOpen=true&page=1&pageSize=20
```

Active restaurants with owners are public, including closed restaurants with
`isOpen:false`. Inactive, deleted and unowned restaurants return 404 for profile
and menu requests and are omitted from search. Sold-out dishes remain visible
with `isAvailable:false`; deleted dishes are omitted.

Both public lists and the owner menu list accept `page` (1–10000) and `pageSize`
(1–100); defaults are 1 and 20. Lists are ordered by name and ID for consistent
paging, and return `items`, `pageNumber`, `pageSize` and `totalCount`. An empty
restaurant menu returns 200 with an empty `items` array; a hidden or nonexistent
restaurant returns 404.

There is no customer checkout or visual frontend in this milestone. Menu prices
are item prices, not final totals including taxes.
