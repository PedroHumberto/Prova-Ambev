namespace Ambev.DeveloperEvaluation.Domain.Sales.Exceptions;

public abstract class SalesDomainException : global::DomainException
{
    protected SalesDomainException(string message)
        : base(message)
    {
    }

    protected SalesDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class InvalidSaleException : SalesDomainException
{
    public InvalidSaleException(string message)
        : base(message)
    {
    }
}

public sealed class InvalidSaleItemException : SalesDomainException
{
    public InvalidSaleItemException(string message)
        : base(message)
    {
    }
}

public sealed class DuplicateActiveProductException : SalesDomainException
{
    public DuplicateActiveProductException(Guid productId)
        : base($"An active item for product '{productId}' already exists in the sale.")
    {
    }
}

public sealed class DuplicateSaleNumberException : SalesDomainException
{
    public DuplicateSaleNumberException(string saleNumber)
        : base($"A sale with number '{saleNumber}' already exists.")
    {
    }

    public DuplicateSaleNumberException(string saleNumber, Exception innerException)
        : base($"A sale with number '{saleNumber}' already exists.", innerException)
    {
    }
}

public sealed class CancelledSaleModificationException : SalesDomainException
{
    public CancelledSaleModificationException(Guid saleId)
        : base($"Sale '{saleId}' is cancelled and cannot be modified.")
    {
    }
}

public sealed class CancelledSaleItemModificationException : SalesDomainException
{
    public CancelledSaleItemModificationException(Guid saleItemId)
        : base($"Sale item '{saleItemId}' is cancelled and cannot be modified.")
    {
    }
}

public sealed class SaleItemNotFoundException : SalesDomainException
{
    public SaleItemNotFoundException(Guid saleItemId)
        : base($"Sale item '{saleItemId}' does not belong to the sale.")
    {
    }
}

public sealed class LastActiveSaleItemException : SalesDomainException
{
    public LastActiveSaleItemException()
        : base("The last active sale item cannot be cancelled. Cancel the sale instead.")
    {
    }
}

public sealed class MonetaryValueOutOfRangeException : SalesDomainException
{
    public MonetaryValueOutOfRangeException(string valueName)
        : base($"The monetary value '{valueName}' exceeds the numeric({MonetaryConstraints.Precision},{MonetaryConstraints.Scale}) range.")
    {
    }

    public MonetaryValueOutOfRangeException(string valueName, Exception innerException)
        : base($"The monetary value '{valueName}' exceeds the numeric({MonetaryConstraints.Precision},{MonetaryConstraints.Scale}) range.", innerException)
    {
    }
}
