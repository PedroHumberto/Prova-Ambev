# System Overview

## Main flow

```text
HTTP
  -> WebApi model binding and validation
  -> controller
  -> AutoMapper/MediatR
  -> ValidationBehavior
  -> Application handler
  -> Domain aggregate
  -> repository and IUnitOfWork
  -> EF Core/PostgreSQL
  -> HTTP response
```

Sales mutations also create domain events. `DefaultContext` transforms these events into outbox records in the same `SaveChanges`; a worker publishes the messages to RabbitMQ.

## Components

- **WebApi:** controllers, DTOs, HTTP validators, Swagger, middleware, and exception mapping.
- **Application:** commands, queries, handlers, validators, and use-case profiles.
- **Domain:** the `Sale` aggregate, business rules, events, and repository contracts.
- **ORM:** `DefaultContext`, mappings, repositories, migrations, and the PostgreSQL outbox.
- **IoC:** dependency composition, Rebus/RabbitMQ, and the dispatch worker.
- **Common:** security, logging, validation, and health checks.
- **Contracts:** versioned integration contracts.
- **Frontend:** Angular application with authentication, guard, interceptor, and Sales, Users, and Catalog screens.

## Dependencies

```text
WebApi -> IoC -> Application -> Domain
             \-> ORM ---------> Domain
```

The `Domain` project references `Common`. This is a relevant architectural exception because `Common` also references ASP.NET Core infrastructure, logging, and security. The Sales aggregate remains behaviorally isolated, but the Domain assembly is not fully framework-independent.

## Startup

1. The API loads configuration and registers services.
2. Startup applies pending migrations with retries for transient database failures.
3. Compose can enable demonstration data through `DemoData__Enabled`.
4. The application starts the server and the outbox worker.

PostgreSQL and RabbitMQ are actual runtime dependencies. MongoDB and Redis are provisioned by Compose but are not used by the application.
