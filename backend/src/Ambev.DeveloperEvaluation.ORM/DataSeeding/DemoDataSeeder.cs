using Ambev.DeveloperEvaluation.Common.Security;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Enums;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ambev.DeveloperEvaluation.ORM.DataSeeding;

public sealed class DemoDataSeeder
{
    public const string DemoUserEmail = "demo.admin@ambev.local";
    public const string DemoSaleNumber = "DEMO-SALE-001";

    private const string DemoUserPassword = "DemoPassword1!";

    private static readonly string[] DemoSaleNumbers =
    [
        DemoSaleNumber,
        "DEMO-SALE-002",
        "DEMO-SALE-003",
        "DEMO-SALE-004",
        "DEMO-SALE-005",
        "DEMO-SALE-006"
    ];

    private static readonly DateTime DemoSaleDate =
        new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly DefaultContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        DefaultContext context,
        IPasswordHasher passwordHasher,
        ILogger<DemoDataSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var existingUser = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == DemoUserEmail, cancellationToken);

        EnsureExistingDemoUserIsCompatible(existingUser);

        var existingSaleNumbers = await _context.Sales
            .AsNoTracking()
            .Where(sale => DemoSaleNumbers.Contains(sale.SaleNumber))
            .Select(sale => sale.SaleNumber)
            .ToListAsync(cancellationToken);

        var existingSaleNumberSet = existingSaleNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userCreated = existingUser is null;
        if (userCreated)
            await _context.Users.AddAsync(CreateDemoUser(), cancellationToken);

        var salesCreated = 0;
        foreach (var sale in CreateDemoSales())
        {
            if (existingSaleNumberSet.Contains(sale.SaleNumber))
                continue;

            sale.ClearDomainEvents();
            await _context.Sales.AddAsync(sale, cancellationToken);
            salesCreated++;
        }

        await _context.CommitAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Demo data seeding completed. User created: {UserCreated}; sales created: {SalesCreated}",
            userCreated,
            salesCreated);
    }

    private void EnsureExistingDemoUserIsCompatible(User? existingUser)
    {
        if (existingUser is null)
            return;

        if (existingUser.Status != UserStatus.Active ||
            existingUser.Role != UserRole.Admin ||
            !_passwordHasher.VerifyPassword(DemoUserPassword, existingUser.Password))
        {
            throw new InvalidOperationException(
                $"The existing demo user '{DemoUserEmail}' is incompatible with the documented demo credentials.");
        }
    }

    private User CreateDemoUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Username = "Demo Administrator",
            Email = DemoUserEmail,
            Phone = "+5511999999999",
            Password = _passwordHasher.HashPassword(DemoUserPassword),
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static IReadOnlyCollection<Sale> CreateDemoSales()
    {
        return
        [
            Sale.Create(
                DemoSaleNumber,
                DemoSaleDate,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Demo Customer",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Demo Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333333"),
                        "Demo Product - No Discount",
                        2,
                        10.00m),
                    new SaleItemInput(
                        Guid.Parse("44444444-4444-4444-4444-444444444444"),
                        "Demo Product - 10 Percent Discount",
                        5,
                        20.00m),
                    new SaleItemInput(
                        Guid.Parse("55555555-5555-5555-5555-555555555555"),
                        "Demo Product - 20 Percent Discount",
                        10,
                        30.00m)
                ]),
            Sale.Create(
                "DEMO-SALE-002",
                DemoSaleDate.AddDays(-1),
                Guid.Parse("11111111-1111-1111-1111-111111111112"),
                "Ana Souza",
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Demo Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333336"),
                        "Pilsen 350ml",
                        3,
                        5.50m)
                ]),
            Sale.Create(
                "DEMO-SALE-003",
                DemoSaleDate.AddDays(-2),
                Guid.Parse("11111111-1111-1111-1111-111111111113"),
                "Bruno Lima",
                Guid.Parse("22222222-2222-2222-2222-222222222223"),
                "Campinas Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333337"),
                        "Lager 600ml",
                        6,
                        11.90m)
                ]),
            Sale.Create(
                "DEMO-SALE-004",
                DemoSaleDate.AddDays(-3),
                Guid.Parse("11111111-1111-1111-1111-111111111114"),
                "Carla Mendes",
                Guid.Parse("22222222-2222-2222-2222-222222222224"),
                "Rio de Janeiro Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333338"),
                        "IPA 355ml",
                        12,
                        9.75m)
                ]),
            Sale.Create(
                "DEMO-SALE-005",
                DemoSaleDate.AddDays(-4),
                Guid.Parse("11111111-1111-1111-1111-111111111115"),
                "Diego Alves",
                Guid.Parse("22222222-2222-2222-2222-222222222225"),
                "Belo Horizonte Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333339"),
                        "Wheat Beer 500ml",
                        2,
                        14.25m)
                ]),
            Sale.Create(
                "DEMO-SALE-006",
                DemoSaleDate.AddDays(-5),
                Guid.Parse("11111111-1111-1111-1111-111111111116"),
                "Elisa Rocha",
                Guid.Parse("22222222-2222-2222-2222-222222222226"),
                "Curitiba Branch",
                [
                    new SaleItemInput(
                        Guid.Parse("33333333-3333-3333-3333-333333333340"),
                        "Stout 355ml",
                        20,
                        12.80m)
                ])
        ];
    }
}
