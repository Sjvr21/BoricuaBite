# ADR-002: Model Address as a Value Object

## Status

Accepted

## Date

2026-07-31

---

## Context

Restaurants in BoricuaBite require a physical business location.

This location is used to:

- Display restaurant information
- Show business location to customers
- Support future map integration
- Support nearby restaurant searches
- Enable future delivery and pickup features

An address is descriptive information rather than an independently managed business object.

---

## Problem

During the domain modeling phase, we needed to determine whether an address should be represented as:

- An Entity with its own identity and lifecycle
- A Value Object owned by another entity

---

## Decision

Address will be modeled as an immutable Value Object.

It will be owned by entities such as Restaurant rather than existing independently.

The Address object will not have its own identifier.

Two addresses containing identical values represent the same conceptual address.

---

## Rationale

An Address does not exist independently within the business domain.

It cannot:

- Log into the system
- Be managed independently
- Exist without belonging to another entity

Because an Address has no identity or lifecycle of its own, it naturally fits the definition of a Value Object.

Using a Value Object also encourages immutability and keeps related address information grouped together.

---

## Business Rules

The following rules currently apply:

- Address Line 1 is required.
- Address Line 2 is optional.
- City is required.
- State or Territory is required.
- Postal Code is required.
- Country is required.
- Leading and trailing whitespace is removed.
- Invalid addresses cannot be created.
- Restaurant addresses must represent physical locations.
- P.O. Boxes are not permitted for restaurant addresses.

---

## Future Considerations

Address intentionally does not include geographic coordinates.

Future versions of BoricuaBite will introduce a separate concept for geolocation, allowing features such as:

- Interactive maps
- Nearby restaurant searches
- Distance calculations
- Delivery radius validation

Separating geolocation from the postal address keeps each concept focused on a single responsibility.

---

## Consequences

### Benefits

- Clear domain model
- Immutable address information
- Easier testing
- Better maintainability
- No unnecessary Address table
- Aligns with Domain-Driven Design principles

### Trade-offs

- Updating an address requires creating a new Address instance.
- Additional Entity Framework configuration will be required when persistence is implemented.

---

## Alternatives Considered

### Address as an Entity

Rejected.

Although technically possible, an Address has no independent identity within the BoricuaBite domain.

Creating an Address entity would introduce unnecessary complexity without providing business value.

---

## References

- Domain-Driven Design (Eric Evans)
- Microsoft Learn - Value Objects
- BoricuaBite Engineering Principles