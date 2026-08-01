# Configuration

Configuration keys can be supplied through environment variables using `__` as the section separator.

| Key | Usage | Notes |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL | required to start and migrate |
| `SalesMessaging__ConnectionString` | RabbitMQ/AMQP | required for messaging and readiness |
| `Jwt__SecretKey` | JWT signing | use an external secret with adequate length/entropy |
| `DemoData__Enabled` | demonstration data | enable only in local environments |
| `HttpsRedirection__Enabled` | HTTPS redirection | depends on the execution profile |

## Security

Do not use `appsettings.json` or Compose values outside development. The repository contains PostgreSQL, RabbitMQ, and JWT defaults to simplify evaluation; these are known credentials.

In a real environment, require a secret manager or protected variables, restrict `AllowedHosts`, configure JWT issuer/audience, and require HTTPS. The current implementation does not apply all of these safeguards by default.
