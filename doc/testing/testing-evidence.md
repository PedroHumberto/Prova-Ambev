# Verification Evidence

## Recorded execution

Date: 2026-08-01 UTC.

- SDK: `10.0.302`; all 10 projects target `net10.0`.
- Restore: passed.
- Backend: Release build passed with 0 errors and 0 warnings.
- .NET: 400 Unit, 50 Integration, and 10 Functional tests passed, for 460 total.
- Unit, Integration, and Functional: passed locally in Release.
- Compose: rebuild and startup passed using `mcr.microsoft.com/dotnet/sdk:10.0.302` and `mcr.microsoft.com/dotnet/aspnet:10.0.10`; the running API reported runtime `10.0.10`.
- Runtime: live, ready, and frontend endpoints returned HTTP 200; PostgreSQL and RabbitMQ were healthy.

## Previous coverage baseline

- Total backend: 94.14% lines and 86.06% branches.
- Domain: 97.26% lines and 92.94% branches.
- Application: 98.05% lines and 93.59% branches.
- WebApi: 90.27% lines and 85.27% branches.
- Common: 96.65% lines and 85.34% branches.
- ORM: 92.63% lines and 80.43% branches.
- IoC: 92.61% lines and 66.67% branches.
- Frontend: 70.83% lines, 70% statements, 63.41% branches, and 66.66% functions.

These unchanged numbers are the previous recorded baseline, not coverage from the 2026-08-01 migration verification and not a CI gate. The current workflow restores, builds, and tests the backend but does not publish coverage or enforce a threshold.

## Limits

Results may vary by environment, Docker, parallelism, and external dependencies. Repeat the [local development](../operations/local-development.md) sequence before using these numbers as release evidence.
