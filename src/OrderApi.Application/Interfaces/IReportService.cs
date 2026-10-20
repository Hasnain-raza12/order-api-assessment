using OrderApi.Application.DTOs;

namespace OrderApi.Application.Interfaces;

public interface IReportService
{
    Task<IReadOnlyList<TopProductResponse>> GetTopProductsAsync(int days, CancellationToken ct = default);
}
