using Microsoft.EntityFrameworkCore;
using Ordering.Application.Checkout;
using Ordering.Domain;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure.Repositories;

internal sealed class OrderQueries : IOrderQueries
{
    private readonly OrderingDbContext _db;

    public OrderQueries(OrderingDbContext db) => _db = db;

    public async Task<OrderSearchResultDto> SearchAsync(OrderSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _db.Orders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(criteria.Status) && Enum.TryParse<OrderStatus>(criteria.Status, out var status))
        {
            query = query.Where(o => o.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Order.Total is a computed C# property (Subtotal from Items + ShippingCost + Tax -
        // Discount) — not a mapped column, so it can't appear inside a .Select() projection (EF
        // can't translate it to SQL). Same reasoning as ProductQueries.GetBySlugAsync: load the
        // (paged, so still small) page of entities via Include and compute Total in C# afterwards
        // rather than fight the projection.
        var page = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(o => new OrderSummaryDto(
                o.Id, o.OrderNumber, o.Status.ToString(), o.PaymentStatus.ToString(),
                o.Total.Amount, o.Total.Currency, o.CreatedAtUtc))
            .ToList();

        return new OrderSearchResultDto(items, totalCount, criteria.Page, criteria.PageSize);
    }
}
