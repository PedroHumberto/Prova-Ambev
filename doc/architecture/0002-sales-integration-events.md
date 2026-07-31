# ADR 0002: Sales integration events and transactional outbox

- Status: Accepted
- Date: 2026-07-31
- Scope: TASK-011

## Context

Sales state and integration-event publication use different durable systems.
Publishing directly from a request would either expose uncommitted state or lose
an event when PostgreSQL commits while RabbitMQ is unavailable.

## Decision

The four Sales domain events are mapped to immutable V1 contracts in the
dependency-free `Ambev.DeveloperEvaluation.Contracts` project. Payloads contain
only the event ID, sale/item IDs, and UTC occurrence time. They contain no EF
entities, customer data, credentials, or other sensitive values.

`DefaultContext` creates one outbox row for every pending Sales domain event in
the same `SaveChanges` call and PostgreSQL transaction as the aggregate. Domain
events are cleared only after that save succeeds. A failed transaction therefore
does not leave a publishable row, and a committed sale always has its outbox row.

A background dispatcher claims due rows using PostgreSQL `FOR UPDATE SKIP
LOCKED` and a lease, allowing multiple API instances to operate concurrently.
It publishes each contract through a one-way Rebus/RabbitMQ client and marks it
published only after the send completes. Broker/publication failures retain a
bounded error message and remain eligible for durable retry indefinitely, using
exponential backoff capped at the configured maximum delay. Deterministically
invalid internal outbox types or payloads cannot succeed without data repair, so
they are marked dead-lettered immediately and retained for diagnosis. Rebus does
not provide an outbound retry/error queue for the one-way publisher; PostgreSQL
outbox state is the effective retry and poison-message mechanism.

Delivery is **at least once**, not exactly once. Every contract and message
header carries the stable outbox `EventId`. A process can stop after RabbitMQ
accepts a publication but before PostgreSQL records success; the lease will then
expire and the same event can be published again. Consumers are responsible for
idempotency/deduplication by `EventId`.

## Consequences

- RabbitMQ failure after a database commit cannot undo or lose the sale event;
  publication resumes from the durable outbox.
- A rollback cannot publish an event because dispatchers only see committed rows.
- Prolonged broker outages never exhaust publication attempts or discard events.
- Deterministically invalid outbox rows remain observable instead of being
  retried forever or deleted.
- PostgreSQL remains the source of truth for dispatch state, while RabbitMQ is
  the local Rebus transport.
- Exactly-once processing across PostgreSQL, RabbitMQ, and consumers is not
  claimed or provided.
