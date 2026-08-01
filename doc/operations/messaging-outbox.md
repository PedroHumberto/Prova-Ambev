# Messaging and Operations

RabbitMQ is accessed through Rebus. PostgreSQL stores the outbox. MongoDB and Redis appear in Compose but are not used by the current code.

## Recommended monitoring

- number of pending messages;
- age of the oldest message;
- attempts and last errors;
- dead-letter messages;
- claim/publication time;
- expired leases;
- PostgreSQL and RabbitMQ availability.

## Health checks

- `/health/live`: process is alive.
- `/health/ready`: PostgreSQL and RabbitMQ.
- `/health`: all configured checks.

Real readiness does not mean that every Compose component is a dependency: MongoDB and Redis are not checked because they are not used by the application.
