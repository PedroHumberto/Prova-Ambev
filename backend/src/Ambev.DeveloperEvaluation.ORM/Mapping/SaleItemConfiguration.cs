using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems", table =>
        {
            table.HasCheckConstraint(
                "CK_SaleItems_Quantity",
                "\"Quantity\" BETWEEN 1 AND 20");
            table.HasCheckConstraint(
                "CK_SaleItems_DiscountPercentage",
                "\"DiscountPercentage\" IN (0, 10, 20)");
            table.HasCheckConstraint(
                "CK_SaleItems_MonetaryValues",
                "\"UnitPrice\" > 0 AND \"Subtotal\" >= 0 AND \"DiscountAmount\" >= 0 " +
                "AND \"TotalAmount\" >= 0 AND \"DiscountAmount\" <= \"Subtotal\" " +
                "AND \"TotalAmount\" = \"Subtotal\" - \"DiscountAmount\"");
            table.HasCheckConstraint(
                "CK_SaleItems_Status",
                $"\"Status\" IN ('{SaleStatus.Active}', '{SaleStatus.Cancelled}')");
            table.HasCheckConstraint(
                "CK_SaleItems_Cancellation",
                $"(\"Status\" = '{SaleStatus.Active}' AND \"CancelledAt\" IS NULL) OR " +
                $"(\"Status\" = '{SaleStatus.Cancelled}' AND \"CancelledAt\" IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();
        builder.Property<Guid>("SaleId").HasColumnType("uuid");
        builder.Property(item => item.ProductId).HasColumnType("uuid");
        builder.Property(item => item.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Quantity);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.DiscountPercentage);
        builder.Property(item => item.Subtotal).HasPrecision(18, 2);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 2);
        builder.Property(item => item.TotalAmount).HasPrecision(18, 2);
        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(item => item.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(item => item.CancelledAt).HasColumnType("timestamp with time zone");

        builder.HasIndex("SaleId").HasDatabaseName("IX_SaleItems_SaleId");
        builder.HasIndex("SaleId", nameof(SaleItem.ProductId))
            .IsUnique()
            .HasFilter($"\"Status\" = '{SaleStatus.Active}'")
            .HasDatabaseName("UX_SaleItems_SaleId_ProductId_Active");

        builder.Ignore(item => item.IsActive);
    }
}
