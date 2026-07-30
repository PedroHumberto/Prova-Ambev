# Repository Guide

## Working Directory

- The repository root is only a container; run all .NET and Docker commands from `backend/`.
- The solution targets .NET 8 and has no pinned SDK (`global.json`).

## Verification

Run in this order from `backend/`:

```powershell
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release --no-build --no-restore
```

- Only `tests/Ambev.DeveloperEvaluation.Unit` currently contains tests; the Integration and Functional projects are empty placeholders.
- Focus a test class with `dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --filter "FullyQualifiedName~CreateUserHandlerTests"`.
- Focus Sales domain tests with `dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --filter "FullyQualifiedName~Domain.Sales"`.
- `dotnet format Ambev.DeveloperEvaluation.sln --verify-no-changes --no-restore` has pre-existing whitespace failures across source and tests. Use it on touched files, but do not attribute the solution-wide baseline to your change.

## Architecture

- `src/Ambev.DeveloperEvaluation.WebApi/Program.cs` is the composition root. HTTP feature DTOs, request validators, and API AutoMapper profiles live under that project's `Features`; controllers map them to MediatR commands.
- Application use cases live under `src/Ambev.DeveloperEvaluation.Application/<Capability>/<UseCase>` as command/result, validator, profile, and handler sets. MediatR and AutoMapper discover both the WebApi and Application assemblies in `Program.cs`.
- Domain owns entities, repository contracts, and business validation. ORM implements repositories and EF Core mappings/migrations. IoC wires those boundaries through `DependencyResolver.RegisterDependencies`.
- Keep both validation boundaries when extending a feature: WebApi validators reject transport input, while Application validators protect commands sent outside HTTP.

## Binding Sales Conventions

- `Sale` is the aggregate root; mutate its internal `SaleItem` entities only
  through aggregate behavior. Customer, branch, and product names are snapshots.
- Use client-supplied, case-insensitively unique sale numbers and UUID identifiers.
  Cancellation is logical and idempotent; item removal during PUT is cancellation.
- Money uses `decimal`/`numeric(18,2)`. Round each line with
  `MidpointRounding.AwayFromZero` and derive sale totals by summing active rounded
  lines. Never accept discounts, totals, statuses, or audit fields as client truth.
- Keep Sales request DTOs strict, implement the documented full-replacement PUT
  and individual item-cancellation endpoint, and preserve the documented query
  names, whitelist ordering, metadata, statuses, and HTTP failure semantics.
- Full-replacement PUT handlers must call `Sale.ReplaceEditableData` as one atomic
  aggregate operation, not chain header and item mutations. Items and domain
  events remain exposed as read-only collections.
- Serialize mutations of an existing sale with the transactional row lock (or
  equivalent guarantee), loading its items only after locking.

## Database And Runtime

- Persistence is PostgreSQL via Npgsql. The committed `src/Ambev.DeveloperEvaluation.WebApi/appsettings.json` default connection string is SQL Server-shaped and does not work with Npgsql; local runs must override `ConnectionStrings__DefaultConnection` or use the full Compose stack.
- API startup always calls `Database.MigrateAsync`, so it requires a reachable database and applies pending migrations before serving requests.
- `docker compose up --build` expects `HTTPS_CERT_PASSWORD` and the certificate `%APPDATA%/ASP.NET/Https/Ambev.DeveloperEvaluation.WebApi.pfx`. PostgreSQL, MongoDB, and Redis publish dynamically assigned host ports; only the API pins host ports `8080` and `8081`.
- EF migrations belong in `src/Ambev.DeveloperEvaluation.ORM/Migrations`; use WebApi as the startup project and ORM as the migrations project.

## Repository Structure

```text
.
|-- backend/
|   |-- src/
|   |   |-- Ambev.DeveloperEvaluation.Application/
|   |   |-- Ambev.DeveloperEvaluation.Common/
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
