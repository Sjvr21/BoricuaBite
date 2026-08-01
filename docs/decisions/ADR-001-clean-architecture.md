# ADR-001: Use Clean Architecture

## Status

Accepted

## Context

BoricuaBite is a multi-restaurant ordering platform with authentication, menus, orders, payments, reviews, email notifications, and external service integrations.

The system needs a structure that keeps business rules separate from frameworks and infrastructure concerns.

## Decision

The backend will use Clean Architecture with four primary projects:

- `BoricuaBite.Domain`
- `BoricuaBite.Application`
- `BoricuaBite.Infrastructure`
- `BoricuaBite.Api`

The dependency direction will be:

```text
Api -> Application
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Domain
Domain -> no project dependencies