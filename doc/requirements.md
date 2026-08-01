# Requirements and Capabilities

## Implemented capabilities

### Authentication

- Authenticate an active user by email and password.
- Issue a JWT containing identity, name, and role claims.
- Reject invalid credentials without distinguishing a missing user from an incorrect password.

### Users

- Create a user.
- Retrieve a user by UUID.
- Delete a user by UUID.

There are no implemented use cases for listing or updating users.

### Catalog

- Retrieve customers, branches, and products derived from snapshots stored in Sales.
- Filter by text and limit the number of results.

The catalog is not a master-data system and has no independent customer, branch, or product tables.

### Sales

- Create, retrieve, list, and replace a sale.
- Cancel a sale or an item.
- Apply discounts, totals, status, audit dates, and events on the server.
- Filter and order the list using strict query parameters.

## Sales business rules

- A sale must contain at least one active item.
- Active products cannot be duplicated within the same sale.
- Allowed quantity: 1 to 20.
- Unit price must be positive and contain no more than two decimal places.
- Discount: 0% for 1-3, 10% for 4-9, and 20% for 10-20 units.
- Lines are rounded with `MidpointRounding.AwayFromZero`; the total sums already-rounded lines.
- Sale number has a maximum of 50 characters and is unique case-insensitively.
- Cancellation is logical and idempotent.
- The last active item cannot be cancelled.
- `PUT` is a complete replacement; omitted items are cancelled.
- Cancelled items preserve historical values and do not contribute to current totals.

## Observed non-functional requirements

- PostgreSQL with EF Core/Npgsql.
- Transactions and `SELECT ... FOR UPDATE` row locks for Sales mutations.
- Transactional outbox with at-least-once delivery.
- Structured logs, correlation ID, trace ID, and PostgreSQL/RabbitMQ health checks.
- Unit, integration, functional, and frontend tests.

## Known risks

- User creation, retrieval, and deletion do not currently use `[Authorize]`.
- The user creation request accepts `Status` and `Role`, creating a mass-assignment risk.
- Base configuration contains development credentials and a development JWT key.
- JWT validation does not constrain issuer/audience and globally disables HTTPS metadata requirements.
- This repository has no event consumers or deduplication store; external consumers must use `EventId`.
