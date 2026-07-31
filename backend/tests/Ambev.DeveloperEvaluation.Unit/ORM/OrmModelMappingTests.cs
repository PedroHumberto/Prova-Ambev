using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Mapping;
using Ambev.DeveloperEvaluation.ORM.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.ORM;

public sealed class OrmModelMappingTests : IDisposable
{
    private readonly DefaultContext _context = new(new DbContextOptionsBuilder<DefaultContext>()
        .UseNpgsql("Host=localhost;Database=model_metadata;Username=test;Password=test")
        .Options);

    [Fact]
    public void Model_UserConfiguration_DefinesRequiredPropertiesAndUniqueEmailIndex()
    {
        var entity = GetDesignTimeEntity(typeof(User));

        entity.GetTableName().Should().Be("Users");
        entity.FindPrimaryKey()!.Properties.Should().ContainSingle(property => property.Name == nameof(User.Id));
        entity.FindProperty(nameof(User.Id))!.GetColumnType().Should().Be("uuid");
        entity.FindProperty(nameof(User.Id))!.GetDefaultValueSql().Should().Be("gen_random_uuid()");
        AssertRequiredString(entity, nameof(User.Username), 50);
        AssertRequiredString(entity, nameof(User.Password), 100);
        AssertRequiredString(entity, nameof(User.Email), 100);
        entity.FindProperty(nameof(User.Phone))!.GetMaxLength().Should().Be(20);
        entity.FindProperty(nameof(User.Status))!.GetMaxLength().Should().Be(20);
        entity.FindProperty(nameof(User.Role))!.GetMaxLength().Should().Be(20);

        var emailIndex = entity.GetIndexes().Single(index => index.GetDatabaseName() == UserConfiguration.UserEmailUniqueIndexName);
        emailIndex.IsUnique.Should().BeTrue();
        emailIndex.Properties.Should().ContainSingle(property => property.Name == nameof(User.Email));
    }

    [Fact]
    public void Model_SaleConfiguration_DefinesPostgresTypesConstraintsIndexesAndRelationship()
    {
        var entity = GetDesignTimeEntity(typeof(Sale));

        entity.GetTableName().Should().Be("Sales");
        entity.FindProperty(nameof(Sale.Id))!.ValueGenerated.Should().Be(ValueGenerated.Never);
        AssertColumn(entity, nameof(Sale.SaleNumber), "citext", 50);
        AssertColumn(entity, nameof(Sale.CustomerName), "citext", 200);
        AssertColumn(entity, nameof(Sale.BranchName), "citext", 200);
        AssertMoney(entity, nameof(Sale.Subtotal));
        AssertMoney(entity, nameof(Sale.DiscountAmount));
        AssertMoney(entity, nameof(Sale.TotalAmount));
        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().BeEquivalentTo(
            "CK_Sales_SaleNumber_Length",
            "CK_Sales_MonetaryValues",
            "CK_Sales_Status",
            "CK_Sales_Cancellation");

        var numberIndex = entity.GetIndexes().Single(index => index.GetDatabaseName() == SaleConfiguration.SaleNumberUniqueIndexName);
        numberIndex.IsUnique.Should().BeTrue();
        var pagingIndex = entity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_Sales_SaleDate_Id");
        pagingIndex.Properties.Select(property => property.Name).Should().Equal(nameof(Sale.SaleDate), nameof(Sale.Id));
        pagingIndex.IsDescending.Should().Equal(true, false);

        var itemsNavigation = entity.FindNavigation(nameof(Sale.Items))!;
        itemsNavigation.GetPropertyAccessMode().Should().Be(PropertyAccessMode.Field);
        itemsNavigation.ForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        itemsNavigation.ForeignKey.Properties.Should().ContainSingle(property => property.Name == "SaleId");
        entity.FindProperty(nameof(Sale.DomainEvents)).Should().BeNull();
        entity.FindProperty(nameof(Sale.IsActive)).Should().BeNull();
    }

    [Fact]
    public void Model_SaleItemConfiguration_DefinesChecksMoneyAndActiveProductUniqueness()
    {
        var entity = GetDesignTimeEntity(typeof(SaleItem));

        entity.GetTableName().Should().Be("SaleItems");
        entity.FindProperty(nameof(SaleItem.Id))!.ValueGenerated.Should().Be(ValueGenerated.Never);
        entity.FindProperty("SaleId")!.GetColumnType().Should().Be("uuid");
        AssertRequiredString(entity, nameof(SaleItem.ProductName), 200);
        AssertMoney(entity, nameof(SaleItem.UnitPrice));
        AssertMoney(entity, nameof(SaleItem.Subtotal));
        AssertMoney(entity, nameof(SaleItem.DiscountAmount));
        AssertMoney(entity, nameof(SaleItem.TotalAmount));
        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().BeEquivalentTo(
            "CK_SaleItems_Quantity",
            "CK_SaleItems_DiscountPercentage",
            "CK_SaleItems_MonetaryValues",
            "CK_SaleItems_Status",
            "CK_SaleItems_Cancellation");

        var activeProductIndex = entity.GetIndexes()
            .Single(index => index.GetDatabaseName() == "UX_SaleItems_SaleId_ProductId_Active");
        activeProductIndex.IsUnique.Should().BeTrue();
        activeProductIndex.Properties.Select(property => property.Name)
            .Should().Equal("SaleId", nameof(SaleItem.ProductId));
        activeProductIndex.GetFilter().Should().Be("\"Status\" = 'Active'");
        entity.FindProperty(nameof(SaleItem.IsActive)).Should().BeNull();
    }

    [Fact]
    public void Model_OutboxConfiguration_DefinesJsonChecksAndPendingIndex()
    {
        var entity = GetDesignTimeEntity(typeof(OutboxMessage));

        entity.GetTableName().Should().Be("OutboxMessages");
        entity.FindProperty(nameof(OutboxMessage.Id))!.ValueGenerated.Should().Be(ValueGenerated.Never);
        AssertRequiredString(entity, nameof(OutboxMessage.Type), 200);
        entity.FindProperty(nameof(OutboxMessage.Payload))!.GetColumnType().Should().Be("jsonb");
        entity.FindProperty(nameof(OutboxMessage.Payload))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(OutboxMessage.LastError))!.GetMaxLength().Should().Be(2000);
        entity.GetCheckConstraints().Select(constraint => constraint.Name).Should().BeEquivalentTo(
            "CK_OutboxMessages_Attempts",
            "CK_OutboxMessages_FinalState");

        var pendingIndex = entity.GetIndexes().Single(index => index.GetDatabaseName() == "IX_OutboxMessages_Pending");
        pendingIndex.Properties.Select(property => property.Name).Should().Equal(
            nameof(OutboxMessage.NextAttemptAt),
            nameof(OutboxMessage.CreatedAt));
        pendingIndex.GetFilter().Should().Be("\"PublishedAt\" IS NULL AND \"DeadLetteredAt\" IS NULL");
    }

    public void Dispose() => _context.Dispose();

    private IEntityType GetDesignTimeEntity(Type type) =>
        _context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;

    private static void AssertRequiredString(IEntityType entity, string propertyName, int maxLength)
    {
        var property = entity.FindProperty(propertyName)!;
        property.IsNullable.Should().BeFalse();
        property.GetMaxLength().Should().Be(maxLength);
    }

    private static void AssertColumn(IEntityType entity, string propertyName, string columnType, int maxLength)
    {
        var property = entity.FindProperty(propertyName)!;
        property.IsNullable.Should().BeFalse();
        property.GetColumnType().Should().Be(columnType);
        property.GetMaxLength().Should().Be(maxLength);
    }

    private static void AssertMoney(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName)!;
        property.GetPrecision().Should().Be(18);
        property.GetScale().Should().Be(2);
    }
}
