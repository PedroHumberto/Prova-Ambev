# Ambev Developer Evaluation

Backend API for the Ambev Developer Evaluation, built with .NET 8, PostgreSQL, Rebus, and RabbitMQ. The solution follows a layered architecture and provides user management, authentication, and sales.

## Technology Stack

- .NET 8 and ASP.NET Core Web API
- PostgreSQL and Entity Framework Core
- Rebus and RabbitMQ with a transactional PostgreSQL outbox
- MediatR and AutoMapper
- FluentValidation
- JWT authentication
- Serilog
- xUnit, NSubstitute, FluentAssertions, Bogus, and Coverlet
- Docker Compose

## Repository Structure

```text
.
|-- backend/
|   |-- src/
|   |   |-- Ambev.DeveloperEvaluation.Application/
|   |   |-- Ambev.DeveloperEvaluation.Common/
|   |   |-- Ambev.DeveloperEvaluation.Contracts/
|   |   |-- Ambev.DeveloperEvaluation.Domain/
|   |   |-- Ambev.DeveloperEvaluation.IoC/
|   |   |-- Ambev.DeveloperEvaluation.ORM/
|   |   `-- Ambev.DeveloperEvaluation.WebApi/
|   `-- tests/
|       |-- Ambev.DeveloperEvaluation.Functional/
|       |-- Ambev.DeveloperEvaluation.Integration/
|       `-- Ambev.DeveloperEvaluation.Unit/
|-- .github/workflows/
`-- global.json
```

All .NET and Docker commands in this document must be run from `backend/`.

## Prerequisites

Install [.NET SDK 8.0.423](https://dotnet.microsoft.com/download/dotnet/8.0).
[Docker Desktop](https://www.docker.com/products/docker-desktop/) must also be
running for the Integration and Functional test projects, which provision real
PostgreSQL and RabbitMQ dependencies with Testcontainers.

The repository-level `global.json` pins the SDK to the installed .NET 8 feature band and permits newer patches in that band. Confirm the selected SDK with:

```powershell
dotnet --version
dotnet --list-runtimes
```

## Build and Test

From `backend/`, run:

```powershell
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release --no-build --no-restore
```

Current verification on 2026-07-31: restore and the Release build succeeded, and
all 335 Unit, Integration, and Functional tests passed. Functional tests run the
real HTTP pipeline through `WebApplicationFactory`, JWT authentication, and a
PostgreSQL Testcontainer. The command sequence above is the single supported way
to validate the complete solution.

To generate a coverage report, use `coverage-report.bat` on Windows or `coverage-report.sh` on Linux and macOS.

## Run with Docker Compose

The default Compose profile uses HTTP inside the local Docker network, so it does
not require a host-specific certificate or secret. From `backend/`, run:

```powershell
docker compose up --build
```

The API waits for PostgreSQL before applying migrations. If the database is still
starting, migration attempts are retried after 10, 30, 50, 60, and 90 seconds;
permanent database or schema errors still stop the API.

The services are then available at:

- Angular frontend: `http://localhost:4200`
- Swagger UI: `http://localhost:8080/swagger`
- API over HTTP: `http://localhost:8080`
- API through the frontend proxy: `http://localhost:4200/api`
- Health checks: `http://localhost:8080/health`, `/health/live`, and `/health/ready`
- RabbitMQ management UI: use the dynamically assigned host port shown by `docker compose ps`

Stop the stack with:

```powershell
docker compose down
```

Use `docker compose down --volumes` only when the local database data may be deleted.

## Run Locally

The API uses PostgreSQL through Npgsql, applies pending migrations during startup,
and dispatches committed Sales outbox rows through RabbitMQ. Provide reachable
PostgreSQL and RabbitMQ instances when running outside Compose.

From `backend/`, run:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=developer_evaluation;Username=developer;Password=your-password"
$env:SalesMessaging__ConnectionString = "amqp://developer:development@localhost:5672"
$env:Jwt__SecretKey = "replace-with-a-development-key-at-least-32-bytes-long"
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi
```

With the default launch profile, Swagger is available at `http://localhost:5119/swagger` in the Development environment.

Do not commit real credentials. Use environment variables, .NET User Secrets, or a secret manager for local and deployed environments.

Sales integration-event delivery is at least once. Contracts and Rebus headers
carry a stable `EventId`; consumers must deduplicate with that ID. Publication
failures remain in the PostgreSQL outbox for unlimited durable retries with
capped backoff. Only deterministically invalid internal outbox data is retained
as dead-lettered. Rebus's one-way publisher does not use a RabbitMQ error queue.
See `doc/architecture/0002-sales-integration-events.md` for details.

## Database Migrations

Entity Framework Core migrations belong to `src/Ambev.DeveloperEvaluation.ORM/Migrations`. Use the Web API as the startup project:

```powershell
dotnet ef migrations add MigrationName --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
dotnet ef database update --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
```

## Git Flow

The repository uses the following branch flow:

1. Create work branches from `develop` using the `feature/<short-description>` pattern.
2. Open pull requests from `feature/*` to `develop`.
3. Promote releases through a pull request from `develop` to `main`.
4. Do not open pull requests directly from `feature/*` to `main`.

GitHub Actions validates these transitions and automatically opens the expected pull requests when feature and develop branches are pushed.

## Commit Convention

Commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```text
<type>(<optional-scope>): <short imperative description>
```

Allowed types:

- `feat`: add or change user-visible functionality
- `fix`: correct a defect
- `docs`: update documentation only
- `test`: add or update tests
- `refactor`: restructure code without changing behavior
- `perf`: improve performance
- `build`: change build tooling or dependencies
- `ci`: change continuous integration configuration
- `chore`: perform repository maintenance
- `revert`: revert a previous commit

Examples:

```text
feat(sales): add quantity discount rules
fix(auth): reject expired tokens
docs: document local PostgreSQL setup
```

Use `!` and a `BREAKING CHANGE:` footer when a commit introduces an incompatible change.

## API Documentation

Swagger is enabled in the Development environment. Additional endpoint references are available under `.doc/` in the repository when working with the complete project materials.

The Sales aggregate is implemented in
`backend/src/Ambev.DeveloperEvaluation.Domain/Sales`. PostgreSQL persistence,
EF Core mappings and migrations are implemented under
`backend/src/Ambev.DeveloperEvaluation.ORM`, and the MediatR use cases are under
`backend/src/Ambev.DeveloperEvaluation.Application/Sales`. Sales HTTP endpoints,
including filtered pagination and deterministic whitelisted ordering, are implemented under
`backend/src/Ambev.DeveloperEvaluation.WebApi/Features/Sales`. Unit tests and
PostgreSQL/Testcontainers integration tests cover these layers, including WebApi
unit tests.

Functional API tests cover users, authentication, the complete Sales lifecycle,
query behavior, authorization, and error contracts. The coverage gate remains
pending for TASK-014.
