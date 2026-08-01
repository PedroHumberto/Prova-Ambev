# Technology Stack

- .NET 10 / ASP.NET Core Web API; all 10 projects target `net10.0`
- Entity Framework Core / Npgsql / PostgreSQL
- MediatR, AutoMapper, and FluentValidation
- JWT and BCrypt
- Serilog
- Rebus and RabbitMQ
- PostgreSQL transactional outbox
- xUnit, NSubstitute, FluentAssertions, Bogus, Testcontainers, and WebApplicationFactory
- Angular 20, TypeScript, Jasmine/Karma, and Nginx
- Docker Compose

Backend container builds use `mcr.microsoft.com/dotnet/sdk:10.0.302`; the API uses `mcr.microsoft.com/dotnet/aspnet:10.0.10` at runtime.

MongoDB and Redis are provisioned by Compose but are not used by the current application.
