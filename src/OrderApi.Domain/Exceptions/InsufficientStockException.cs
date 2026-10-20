namespace OrderApi.Domain.Exceptions;

public class InsufficientStockException : Exception
{
    public IReadOnlyList<StockShortfall> Shortfalls { get; }

    public InsufficientStockException(IReadOnlyList<StockShortfall> shortfalls)
        : base("One or more products do not have sufficient stock.")
    {
        Shortfalls = shortfalls;
    }
}

public record StockShortfall(int ProductId, int Requested, int Available);
