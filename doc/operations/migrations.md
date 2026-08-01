# Migrations

Migrations are stored in `backend/src/Ambev.DeveloperEvaluation.ORM/Migrations`. The startup project is `Ambev.DeveloperEvaluation.WebApi`.

## Commands

Run from `backend/`:

```powershell
dotnet ef migrations add MigrationName --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
dotnet ef database update --project src/Ambev.DeveloperEvaluation.ORM --startup-project src/Ambev.DeveloperEvaluation.WebApi
```

## Startup

The API runs `Database.MigrateAsync()` before serving requests and retries transient connection failures. Structural failures prevent startup.

In multi-instance environments, automatic migration application must be evaluated operationally to avoid deployment races and unplanned rollback behavior.
