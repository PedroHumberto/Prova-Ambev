# Verification Evidence

## Recorded execution

Date: 2026-07-31. Environment reported by the analysis agents:

- SDK: `8.0.423`.
- Docker: `29.6.2`.
- Backend: Release build passed with 0 errors and 0 warnings.
- .NET: 400 Unit, 50 Integration, and 10 Functional tests passed.
- Frontend: tests, lint, and build passed.

## Recorded coverage

- Total backend: 94.14% lines and 86.06% branches.
- Domain: 97.26% lines and 92.94% branches.
- Application: 98.05% lines and 93.59% branches.
- WebApi: 90.27% lines and 85.27% branches.
- Common: 96.65% lines and 85.34% branches.
- ORM: 92.63% lines and 80.43% branches.
- IoC: 92.61% lines and 66.67% branches.
- Frontend: 70.83% lines, 70% statements, 63.41% branches, and 66.66% functions.

These numbers are evidence from one execution, not a CI gate. The current workflow restores, builds, and tests the backend but does not publish coverage or enforce a threshold.

## Limits

Results may vary by environment, Docker, parallelism, and external dependencies. Repeat the [local development](../operations/local-development.md) sequence before using these numbers as release evidence.
