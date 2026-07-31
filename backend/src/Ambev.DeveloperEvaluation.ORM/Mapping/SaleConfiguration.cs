using Ambev.DeveloperEvaluation.Domain.Sales.Entities;
using Ambev.DeveloperEvaluation.Domain.Sales.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ambev.DeveloperEvaluation.ORM.Mapping;

public sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public const string SaleNumberUniqueIndexName = "UX_Sales_SaleNumber";

    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", table =>
        {
            table.HasCheckConstraint(
                "CK_Sales_SaleNumber_Length",
                "char_length(\"SaleNumber\") BETWEEN 1 AND 50");
            table.HasCheckConstraint(
                "CK_Sales_MonetaryValues",
                "\"Subtotal\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TotalAmount\" >= 0 " +
                "AND \"DiscountAmount\" <= \"Subtotal\" " +
                "AND \"TotalAmount\" = \"Subtotal\" - \"DiscountAmount\"");
            table.HasCheckConstraint(
                "CK_Sales_Status",
                $"\"Status\" IN ('{SaleStatus.Active}', '{SaleStatus.Cancelled}')");
            table.HasCheckConstraint(
                "CK_Sales_Cancellation",
                $"(\"Status\" = '{SaleStatus.Active}' AND \"CancelledAt\" IS NULL) OR " +
                $"(\"Status\" = '{SaleStatus.Cancelled}' AND \"CancelledAt\" IS NOT NULL)");
        });

        builder.HasKey(sale => sale.Id);
        builder.Property(sale => sale.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(sale => sale.SaleNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("citext");

        builder.Property(sale => sale.SaleDate).HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.CustomerId).HasColumnType("uuid");
        builder.Property(sale => sale.CustomerName).IsRequired().HasMaxLength(200);
        builder.Property(sale => sale.BranchId).HasColumnType("uuid");
        builder.Property(sale => sale.BranchName).IsRequired().HasMaxLength(200);
        builder.Property(sale => sale.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(sale => sale.Subtotal).HasPrecision(18, 2);
        builder.Property(sale => sale.DiscountAmount).HasPrecision(18, 2);
        builder.Property(sale => sale.TotalAmount).HasPrecision(18, 2);

        builder.Property(sale => sale.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.UpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(sale => sale.CancelledAt).HasColumnType("timestamp with time zone");

        builder.HasIndex(sale => sale.SaleNumber)
            .IsUnique()
            .HasDatabaseName(SaleNumberUniqueIndexName);
        builder.HasIndex(sale => new { sale.SaleDate, sale.Id })
            .IsDescending(true, false)
            .HasDatabaseName("IX_Sales_SaleDate_Id");
        builder.HasIndex(sale => sale.CustomerId).HasDatabaseName("IX_Sales_CustomerId");
        builder.HasIndex(sale => sale.BranchId).HasDatabaseName("IX_Sales_BranchId");
        builder.HasIndex(sale => sale.Status).HasDatabaseName("IX_Sales_Status");

        builder.HasMany(sale => sale.Items)
            .WithOne()
            .HasForeignKey("SaleId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(sale => sale.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(sale => sale.DomainEvents);
        builder.Ignore(sale => sale.IsActive);
    }
}
