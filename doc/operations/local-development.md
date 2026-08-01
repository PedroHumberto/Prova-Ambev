# Local Development

## Prerequisites

- .NET SDK 8.0.423 or a compatible patch according to `global.json`.
- Docker Desktop for integration/functional tests and Compose.
- Node.js/npm for the frontend.

## Backend

Run .NET commands from `backend/`:

```powershell
dotnet restore Ambev.DeveloperEvaluation.sln
dotnet build Ambev.DeveloperEvaluation.sln --configuration Release --no-restore
dotnet test Ambev.DeveloperEvaluation.sln --configuration Release --no-build --no-restore
dotnet run --project src/Ambev.DeveloperEvaluation.WebApi
```

The API requires reachable PostgreSQL and RabbitMQ instances. Swagger is available in Development.

## Compose

```powershell
docker compose up --build
docker compose down
```

Typical Compose ports are frontend `4200`, API `8080`, and Swagger at `8080/swagger`. PostgreSQL and RabbitMQ host ports may be assigned dynamically; check `docker compose ps`.

Compose enables demonstration data through `DemoData__Enabled=true`. Demonstration credentials are public and development-only.

## Frontend

Run commands from `frontend/`:

```powershell
npm ci
npm run start
npm test -- --no-progress
npm run lint
npm run build
```

During development, the proxy points to the local API. In Compose, Nginx forwards `/api` to the WebApi service.
