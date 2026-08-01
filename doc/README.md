# System Documentation

This folder describes the repository's implemented state as of 2026-08-01. The code and automated tests are the source of truth whenever documentation differs from behavior.

## Navigation

- [Overview](overview.md)
- [Requirements and capabilities](requirements.md)
- [System overview](system-overview.md)
- [Project structure](project-structure.md)
- [Technology stack](tech-stack.md)
- [Authentication API](api/authentication.md)
- [Users API](api/users.md)
- [Catalog API](api/catalog.md)
- [Sales API](api/sales.md)
- [HTTP errors](api/errors.md)
- [Target contract differences](api/contract-differences.md)
- [Domain model](architecture/domain-model.md)
- [Transactional outbox](architecture/transactional-outbox.md)
- [Local development](operations/local-development.md)
- [Configuration](operations/configuration.md)
- [Migrations](operations/migrations.md)
- [Messaging and operations](operations/messaging-outbox.md)
- [Test strategy](testing/test-strategy.md)
- [Verification evidence](testing/testing-evidence.md)
- [Angular frontend](frontend/angular.md)

## Classification

- **Current:** describes existing endpoints, rules, and components.
- **Risk:** records known unsafe behavior or gaps without claiming they are fixed.
- **Historical:** template material or old diagnostics that must not be used as a contract.

The ADRs that were stored under `.doc/` are not guaranteed to be available in a clean clone. The decisions required to operate the system are reproduced in this folder.

## Last verification

Date: 2026-08-01 UTC. .NET SDK 10.0.302, Release build passed with 0 warnings and 0 errors, and 400 Unit, 50 Integration, and 10 Functional tests passed. Compose and runtime checks also passed. See [verification evidence](testing/testing-evidence.md) for environment details and limits.
