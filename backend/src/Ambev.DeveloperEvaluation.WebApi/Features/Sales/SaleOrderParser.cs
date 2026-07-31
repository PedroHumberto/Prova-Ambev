using Ambev.DeveloperEvaluation.Domain.Repositories;

namespace Ambev.DeveloperEvaluation.WebApi.Features.Sales;

internal static class SaleOrderParser
{
    private static readonly IReadOnlyDictionary<string, SaleSortField> Fields =
        new Dictionary<string, SaleSortField>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = SaleSortField.Id,
            ["saleNumber"] = SaleSortField.SaleNumber,
            ["saleDate"] = SaleSortField.SaleDate,
            ["customerName"] = SaleSortField.CustomerName,
            ["branchName"] = SaleSortField.BranchName,
            ["status"] = SaleSortField.Status,
            ["subtotal"] = SaleSortField.Subtotal,
            ["discountAmount"] = SaleSortField.DiscountAmount,
            ["totalAmount"] = SaleSortField.TotalAmount,
            ["createdAt"] = SaleSortField.CreatedAt,
            ["updatedAt"] = SaleSortField.UpdatedAt
        };

    internal static bool TryParse(string? value, out IReadOnlyList<SaleSortClause> clauses)
    {
        clauses = [];
        if (value is null)
            return true;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parsed = new List<SaleSortClause>();
        var seenFields = new HashSet<SaleSortField>();
        foreach (var rawClause in value.Split(','))
        {
            var parts = rawClause.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 1 or > 2 || !Fields.TryGetValue(parts[0], out var field))
                return false;

            var direction = SortDirection.Ascending;
            if (parts.Length == 2)
            {
                if (parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
                    direction = SortDirection.Descending;
                else if (!parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!seenFields.Add(field))
                return false;
            parsed.Add(new SaleSortClause(field, direction));
        }

        clauses = parsed;
        return true;
    }
}
