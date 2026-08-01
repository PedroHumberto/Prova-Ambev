# Domain Model

## `Sale` aggregate

`Sale` is the aggregate root. The item list and domain events are exposed as read-only collections. `SaleItem` changes occur through sale behavior.

The aggregate controls item and product invariants, discounts and rounding, active totals, cancellation, complete replacement, and domain events.

## Persistence

- IDs are UUIDs.
- Monetary values use `decimal` and `numeric(18,2)` in PostgreSQL.
- Sale number uses case-insensitive uniqueness.
- Duplicate active items are prevented by the domain and reinforced by an index/constraint.
- Read queries use `AsNoTracking`.

## Concurrency

Update and cancellation handlers start a transaction and load the sale with `SELECT ... FOR UPDATE` before loading items. The lock is held until commit to serialize concurrent mutations.
