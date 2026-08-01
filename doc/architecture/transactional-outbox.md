# Transactional Outbox

## Flow

1. The aggregate records a domain event.
2. `DefaultContext` maps the event to a versioned integration contract.
3. The event is saved to `OutboxMessages` in the same `SaveChanges` as the sale.
4. The worker claims a lease using `FOR UPDATE SKIP LOCKED`.
5. The Rebus publisher sends the message to RabbitMQ.
6. Success marks the message as published; failure releases the lease, increments attempts, and applies backoff.

## Guarantee

Delivery is **at-least-once**. Exactly-once is not guaranteed: a failure after publication and before confirmation may cause duplication. `EventId` is stable and must be used by consumers for deduplication.

Transport failures remain in the outbox for durable retries. Deterministically invalid internal payloads may be retained as dead letters. This repository contains no consumers or deduplication store.

## Operations

The default lease is 60 seconds and batch publication is sequential. Large batches or a slow broker may cause reprocessing when the lease expires; monitor volume, age, attempts, and publication time.
