# Test Strategy

## Layers

| Layer | Scope | Dependencies |
|---|---|---|
| Unit | domain, handlers, validators, WebApi, and DI | local process |
| Integration | PostgreSQL, migrations, constraints, locks, concurrency, outbox, and RabbitMQ | Docker/Testcontainers |
| Functional | real HTTP with `WebApplicationFactory`, JWT, and PostgreSQL | Docker/Testcontainers |
| Frontend | services, interceptor, validators, lint, and Angular build | Node/npm |

## Backend commands

```powershell
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release
dotnet test tests/Ambev.DeveloperEvaluation.Unit/Ambev.DeveloperEvaluation.Unit.csproj --configuration Release --filter "FullyQualifiedName~Domain.Sales"
dotnet test tests/Ambev.DeveloperEvaluation.Integration/Ambev.DeveloperEvaluation.Integration.csproj --configuration Release
```

## Known gaps

- The Catalog still has no representative functional HTTP coverage.
- Functional tests remove hosted services, so they do not prove outbox dispatch in the HTTP host.
- CI runs the backend but does not run the frontend, formatting, coverage, or image builds.
- Coverage scripts do not enforce a threshold automatically and use unversioned global tools.
- Coverage excludes some components and migrations; numbers must be interpreted with this limitation.
