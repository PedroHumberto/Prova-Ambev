# Repository Guide

## Scope And Sources Of Truth

- This repository contains a .NET 8 backend evaluation API. Run all .NET, EF Core, coverage, and Docker commands from `backend/` unless a command explicitly targets a root-level file.
- The repository-level `global.json` pins SDK `8.0.423` with `rollForward: latestPatch`. Confirm the active SDK with `dotnet --version` when build behavior differs between environments.
- Treat executable code and automated tests as the source of truth for current behavior. Treat `doc/architecture/0001-sales-functional-contract.md` as the accepted target contract for Sales. Known differences between that ADR and the current HTTP implementation are listed below.
- `README.md` describes setup and project status. `.doc/` contains the original evaluation API references. `doc/`, `TASKS.md`, `ANALISE.md`, and the template files may be workspace-local because they are ignored by Git; do not assume they exist in every clone.
- Never commit production credentials. Values in `appsettings.json` and Compose are development defaults only; use environment variables, User Secrets, or a secret manager outside local development.

## Repository Structure

```text
.
|-- .github/workflows/                         # API validation and Git-flow automation
|-- backend/
|   |-- src/
|   |   |-- Ambev.DeveloperEvaluation.Application/
|   |   |   `-- <Capability>/<UseCase>/         # MediatR commands/queries, handlers, validators
|   |   |-- Ambev.DeveloperEvaluation.Common/  # Security, validation pipeline, logging, health
|   |   |-- Ambev.DeveloperEvaluation.Domain/  # Entities, Sales aggregate, rules, repository contracts
|   |   |-- Ambev.DeveloperEvaluation.IoC/     # Dependency registration
|   |   |-- Ambev.DeveloperEvaluation.ORM/     # EF context, mappings, repositories, migrations
|   |   `-- Ambev.DeveloperEvaluation.WebApi/  # Program, middleware, controllers, HTTP contracts
|   |-- tests/
|   |   |-- Ambev.DeveloperEvaluation.Unit/
|   |   |-- Ambev.DeveloperEvaluation.Integration/
|   |   `-- Ambev.DeveloperEvaluation.Functional/
|   |-- Ambev.DeveloperEvaluation.sln
|   |-- Dockerfile                              # Additional solution-level Dockerfile
|   |-- docker-compose.yml
|   |-- docker-compose.override.yml
|   |-- coverage-report.bat
|   `-- coverage-report.sh
|-- AGENTS.md
|-- README.md
|-- global.json
```

- Compose builds `backend/src/Ambev.DeveloperEvaluation.WebApi/Dockerfile`, not `backend/Dockerfile`.
- Ignore generated `bin/`, `obj/`, coverage output, logs, and IDE metadata while exploring or reviewing changes.

## Project Boundaries

The intended dependency direction is:

```text
WebApi -> IoC -> Application -> Domain -> Common
             `-> ORM ---------> Domain
```

- `WebApi` is the transport layer. Keep controllers, request/response DTOs, transport validators, API AutoMapper profiles, Swagger configuration, and exception-to-HTTP mapping here.
- `Application` owns use cases. A use case normally groups its command or query, result, FluentValidation validator, AutoMapper profile, and MediatR handler under `Application/<Capability>/<UseCase>`.
- `Domain` owns entities, aggregate behavior, business exceptions and validation, domain event records, repository abstractions, and `IUnitOfWork`. It must not depend on Application, ORM, or WebApi.
- `ORM` implements Domain persistence contracts with EF Core and Npgsql. It owns `DefaultContext`, mappings, repositories, and migrations.
- `Common` contains cross-cutting infrastructure such as JWT generation, BCrypt support, MediatR validation behavior, Serilog setup, and health checks. Do not move capability-specific business rules here.
- `IoC` is the composition bridge. Add interface-to-implementation bindings through `DependencyResolver.RegisterDependencies` and the module initializers instead of resolving dependencies manually.

## Composition And Request Flow

- `backend/src/Ambev.DeveloperEvaluation.WebApi/Program.cs` is the composition root.
- FluentValidation, AutoMapper, and MediatR scan both the Application and WebApi assemblies. New handlers, validators, and profiles in those assemblies should not require one-off registration.
- Keep both validation boundaries: WebApi validators protect model binding and HTTP contracts; Application validators protect commands or queries invoked outside HTTP.
- The normal request path is:

```text
HTTP request
-> WebApi model binding and request validation
-> controller and AutoMapper request-to-command mapping
-> MediatR ValidationBehavior
-> Application handler
-> Domain aggregate and repository abstraction
-> IUnitOfWork / EF Core / PostgreSQL
-> result mapping and HTTP response
```

- Successful endpoints currently use `ApiResponse` or `ApiResponseWithData<T>` envelopes. Errors are Problem Details produced by `GlobalExceptionHandler` or status-code pages.
- `GlobalExceptionHandler` currently maps validation to 400, invalid authentication to 401, missing resources to 404, duplicate email or sale number to 409, Sales domain rule failures to 422, and unhandled failures to 500.
- Propagate `CancellationToken` from controllers through handlers and repositories to EF Core calls.

## Implemented Capabilities

### Authentication

- `POST /api/Auth` authenticates by email and BCrypt password, requires an active user, and returns a JWT.
- JWTs contain user identity/name/role claims and currently expire after eight hours.

### Users

- Implemented endpoints: `POST /api/Users`, `GET /api/Users/{id}`, and `DELETE /api/Users/{id}`.
- `ListUsers` and `UpdateUser` are project-folder placeholders, not implemented use cases.
- User endpoints currently do not have `[Authorize]`. Do not assume authentication is enforced without checking the controller.
- User email uniqueness is checked in the use case and enforced by a database unique index. Passwords are persisted as BCrypt hashes.

### Sales

- Sales endpoints are protected by `[Authorize]`.
- Implemented endpoints: create, list, get by ID, full update, cancel sale, and cancel individual item under `backend/src/Ambev.DeveloperEvaluation.WebApi/Features/Sales/SalesController.cs`.
- Application use cases are `CreateSale`, `ListSales`, `GetSaleById`, `UpdateSale`, `CancelSale`, and `CancelSaleItem`.

## Sales Domain Rules

- `Sale` is the aggregate root. Mutate `SaleItem` state only through aggregate behavior. `Items` and `DomainEvents` must remain read-only to callers.
- IDs are UUIDs. `saleNumber` is supplied by the client, trimmed, at most 50 characters, and unique case-insensitively. Customer, branch, and product identity/name values are historical snapshots rather than foreign lookups.
- Sale dates are UTC. Server code owns audit timestamps, statuses, discounts, cancellation data, and totals; never accept those fields as client truth.
- A sale must have at least one active item and cannot contain duplicate active product IDs. Item quantity is 1 through 20 and unit price must be positive with at most two decimal places.
- Discounts are derived from quantity: 1-3 units is 0%, 4-9 is 10%, and 10-20 is 20%.
- Money uses `decimal` in code and `numeric(18,2)` in PostgreSQL. Round each line using `MidpointRounding.AwayFromZero`; derive sale totals by summing active, already-rounded lines.
- Cancellation is logical and idempotent. Cancelled lines retain their values for history but do not contribute to current sale totals.
- PUT is a complete replacement of editable data. It must call `Sale.ReplaceEditableData` once as an atomic aggregate operation. Omitted active items are cancelled; do not implement replacement by chaining independent header/item mutations.
- Mutations of an existing sale must execute in a transaction and acquire the repository row lock, loading items only after `SELECT ... FOR UPDATE`. Preserve equivalent serialization if persistence is refactored.
- Sales domain events are mapped to versioned integration contracts and persisted in a transactional PostgreSQL outbox. A leased background dispatcher publishes them through Rebus/RabbitMQ with at-least-once delivery; consumers must deduplicate by the stable `EventId`.

## Sales Contract Gaps

Do not confuse the accepted ADR target with the current API behavior:

- Current `GET /api/sales` supports `_page` and `_size`; the ADR's `_order` and complete filter set remain pending.
- Current create/update HTTP DTOs use flat `customerId`, `customerName`, `branchId`, and `branchName` fields, while the ADR documents nested `customer` and `branch` objects.
- Current Sales DELETE endpoints return 200 with response envelopes, while the ADR requires 204 without a body.
- Current success responses are wrapped in API envelopes, while ADR examples show the sale/page payload directly.
- Create/update request DTOs reject unknown JSON properties. Preserve strict request handling when aligning their shape with the ADR.
- Query-string rejection rules from the ADR are not fully implemented. When completing the contract, update transport DTOs, validators, mapping, controller behavior, Swagger, and tests together.

## Persistence And Migrations

- Persistence is PostgreSQL through Npgsql. The committed default connection string is PostgreSQL-shaped and usable for the local Compose credentials; override it when using another database or credentials.
- `DefaultContext` enables PostgreSQL `citext`. Sale numbers use it for case-insensitive uniqueness. Monetary columns use precision `(18,2)`, and mappings include constraints and indexes that reinforce domain invariants.
- Repository reads should use `AsNoTracking` unless entities will be changed in the same context.
- API startup always calls `Database.MigrateAsync()` before serving requests. A reachable PostgreSQL instance is therefore mandatory even for startup-only API checks.
- Migrations belong in `backend/src/Ambev.DeveloperEvaluation.ORM/Migrations`. Use ORM as the migrations project and WebApi as the startup project:

```powershell
dotnet ef migrations add MigrationName --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
dotnet ef database update --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
```

- Never edit an applied migration to change current schema behavior. Add a new migration and keep the model snapshot synchronized.

## Runtime And Docker

- Local API command: `dotnet run --project src/Ambev.DeveloperEvaluation.WebApi`.
- Default launch URLs are `http://localhost:5119` and `https://localhost:7181`; Swagger is available only in Development.
- Override configuration with environment variables such as `ConnectionStrings__DefaultConnection` and `Jwt__SecretKey`.
- `docker compose up --build` starts API, PostgreSQL 13, RabbitMQ 4.1, MongoDB 8, and Redis 7.4.1. PostgreSQL and RabbitMQ are integrated into application code; MongoDB and Redis remain provisioned but unused.
- Compose exposes API ports `8080` and `8081`. PostgreSQL, RabbitMQ, MongoDB, and Redis request dynamically assigned host ports rather than pinning their standard ports.
- Compose starts the API over HTTP by default and does not require a host-specific certificate; HTTPS remains available for local profiles when configured explicitly.
- API startup retries transient PostgreSQL migration connection failures with progressive delays before failing permanently.
- Health endpoints are `/health`, `/health/live`, and `/health/ready`. Liveness/readiness checks are currently synthetic and do not prove PostgreSQL, RabbitMQ, Redis, MongoDB, or migration health.
- Serilog writes structured logs to console and, when no debugger is attached, rolling files under `logs/`. There is no configured OpenTelemetry pipeline or DataDog exporter.

## Verification

Run the same sequence used by CI from `backend/`:

```powershell
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release --no-build --no-restore
```

- Unit tests cover Application, Domain, ORM basics, and WebApi contracts/behavior.
- Integration tests are not placeholders. `tests/Ambev.DeveloperEvaluation.Integration/Sales/SalePersistenceTests.cs` uses PostgreSQL Testcontainers and covers migrations, constraints, repository round trips, transactions, row locks, concurrency, and DI. Docker must be available to run them.
- The Functional project is currently an empty placeholder; end-to-end HTTP coverage remains pending.
- Focus a unit test class with:

```powershell
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --filter "FullyQualifiedName~CreateUserHandlerTests"
```

- Focus Sales domain tests with:

```powershell
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --filter "FullyQualifiedName~Domain.Sales"
```

- Run integration tests directly with:

```powershell
dotnet test tests/Ambev.DeveloperEvaluation.Integration/Ambev.DeveloperEvaluation.Integration.csproj --configuration Release
```

- Check formatting with `dotnet format Ambev.DeveloperEvaluation.sln --verify-no-changes --no-restore`. The repository has had solution-wide pre-existing format findings; inspect touched files and do not attribute unrelated baseline findings to the current change.
- Coverage scripts install/use report tooling and remove generated build directories. Review them before execution; they are not a CI coverage gate.

## Change Guidelines

- Prefer the smallest change that preserves layer boundaries and established feature organization.
- Keep transport contracts separate from Domain entities. Map requests to Application commands and results to responses.
- Business-derived values belong in Domain behavior, not controllers, AutoMapper profiles, handlers, or repositories.
- Add or update tests at the owning boundary: aggregate rules in Unit Domain tests, handlers in Unit Application tests, HTTP contracts/controllers in Unit WebApi tests, and PostgreSQL-specific behavior in Integration tests.
- Preserve UTC handling, decimal precision, cancellation idempotency, and concurrency guarantees when changing Sales.
- When behavior intentionally moves toward the Sales ADR, call out any compatibility impact and update all affected layers in the same change.
- Do not introduce MongoDB, Redis, event dispatch, readiness dependencies, or compatibility adapters merely because infrastructure placeholders exist; add them only for a concrete requirement.

## CI And Git Conventions

- `.github/workflows/validate-api.yml` restores, builds, and tests pull requests targeting `develop` or `main` with commands rooted at `backend/`.
- The expected branch flow is `feature/*` to `develop`, then `develop` to `main`. Other workflows validate or automate this flow.
- Use Conventional Commits when asked to commit: `<type>(<optional-scope>): <imperative description>`.
- CI currently does not enforce formatting or a coverage threshold. Passing local format or coverage checks must not be reported as a CI guarantee unless the workflows are updated.
