# Ambev Developer Evaluation

Backend API for the Ambev Developer Evaluation, built with .NET 8 and PostgreSQL. The solution follows a layered architecture and currently provides user management and authentication as the foundation for the sales domain.

## Technology Stack

- .NET 8 and ASP.NET Core Web API
- PostgreSQL and Entity Framework Core
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

Choose one of the following environments:

- [.NET SDK 8.0.423](https://dotnet.microsoft.com/download/dotnet/8.0) and PostgreSQL
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

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

Current stabilization verification on 2026-07-30: restore succeeded, the Release
build completed with 0 warnings and 0 errors, 166 unit tests and 11 PostgreSQL
integration tests passed, and the Functional project remained an empty
placeholder. The NuGet audit reported no vulnerable packages in any
PackageReference project. The solution-level audit still exits with code 1 after
the clean report because `docker-compose.dcproj` uses the unsupported
`package.config` format.

To generate a coverage report, use `coverage-report.bat` on Windows or `coverage-report.sh` on Linux and macOS.

## Run with Docker Compose

The API container requires a trusted ASP.NET Core HTTPS development certificate. On Windows PowerShell, create it before starting the stack:

```powershell
dotnet dev-certs https --clean
dotnet dev-certs https --trust
dotnet dev-certs https -ep "$env:APPDATA\ASP.NET\Https\Ambev.DeveloperEvaluation.WebApi.pfx" -p "development-password"
$env:HTTPS_CERT_PASSWORD = "development-password"
docker compose up --build
```

The services are then available at:

- Swagger UI: `https://localhost:8081/swagger`
- API over HTTP: `http://localhost:8080`
- Health checks: `http://localhost:8080/health`, `/health/live`, and `/health/ready`

Stop the stack with:

```powershell
docker compose down
```

Use `docker compose down --volumes` only when the local database data may be deleted.

## Run Locally

The API uses PostgreSQL through Npgsql and applies pending migrations during startup. Provide a reachable PostgreSQL connection string because the default value in `appsettings.json` is only a template and is not compatible with Npgsql.

From `backend/`, run:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=developer_evaluation;Username=developer;Password=your-password"
$env:Jwt__SecretKey = "replace-with-a-development-key-at-least-32-bytes-long"
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi
```

With the default launch profile, Swagger is available at `http://localhost:5119/swagger` in the Development environment.

Do not commit real credentials. Use environment variables, .NET User Secrets, or a secret manager for local and deployed environments.

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
`backend/src/Ambev.DeveloperEvaluation.ORM`, and the MediatR use cases through
TASK-008 are under `backend/src/Ambev.DeveloperEvaluation.Application/Sales`.
Unit tests and PostgreSQL/Testcontainers integration tests cover these layers.

Sales HTTP endpoints, configurable `_order`, complete filters, functional API
tests, final observability and the coverage gate remain pending from TASK-009
onward.
