using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IPricingService _pricingService;

    public OrderService(AppDbContext context, IPricingService pricingService)
    {
        _context = context;
        _pricingService = pricingService;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, int? creatorUserId = null, UserRole? creatorRole = null)
    {
        // 1. Validate Customer existence
        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.CustomerId);

        if (customer == null)
        {
            throw new KeyNotFoundException($"Customer with ID {request.CustomerId} not found.");
        }

        // 2. Validate Pickup & Drop Areas existence
        var pickupArea = await _context.Areas
            .Include(a => a.Zone)
            .FirstOrDefaultAsync(a => a.Id == request.PickupAreaId);

        if (pickupArea == null)
        {
            throw new KeyNotFoundException($"Pickup area with ID {request.PickupAreaId} not found.");
        }

        var dropArea = await _context.Areas
            .Include(a => a.Zone)
            .FirstOrDefaultAsync(a => a.Id == request.DropAreaId);

        if (dropArea == null)
        {
            throw new KeyNotFoundException($"Drop area with ID {request.DropAreaId} not found.");
        }

        // 3. Reuse PricingService for exact price calculation
        var pricingRequest = new CalculatePriceRequest
        {
            PickupAreaId = request.PickupAreaId,
            DropAreaId = request.DropAreaId,
            Length = request.Length,
            Breadth = request.Breadth,
            Height = request.Height,
            ActualWeight = request.ActualWeight,
            OrderType = request.OrderType,
            PaymentType = request.PaymentType
        };

        var priceResult = await _pricingService.CalculatePriceAsync(pricingRequest);

        // 4. Generate unique tracking number
        string trackingNumber = $"LM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        // 5. Construct Order entity
        var order = new Order
        {
            TrackingNumber = trackingNumber,
            CustomerId = request.CustomerId,
            PickupAreaId = request.PickupAreaId,
            DropAreaId = request.DropAreaId,
            PickupAddress = request.PickupAddress,
            DropAddress = request.DropAddress,

            LengthCm = request.Length,
            WidthCm = request.Breadth,
            HeightCm = request.Height,

            ActualWeightKg = priceResult.ActualWeight,
            VolumetricWeightKg = priceResult.VolumetricWeight,
            ChargeableWeightKg = priceResult.ChargeableWeight,

            OrderType = request.OrderType,
            PaymentType = request.PaymentType,

            RatePerKg = priceResult.RatePerKg,
            DeliveryFee = priceResult.DeliveryFee,
            CODSurcharge = priceResult.CODSurcharge,
            TotalAmount = priceResult.TotalAmount,

            Status = OrderStatus.Created,
            AssignedAgentId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 6. Determine Creator metadata for initial audit trail
        int actorId = creatorUserId ?? request.CustomerId;
        UserRole actorRole = creatorRole ?? UserRole.Customer;
        string notes = (actorRole == UserRole.Admin)
            ? $"Order created by Admin on behalf of Customer '{customer.FullName}'"
            : "Order created";

        // 7. Execute atomic database transaction for Order + initial OrderStatusHistory
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var initialHistory = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatus.Created,
                    ActorId = actorId,
                    ActorRole = actorRole,
                    Notes = notes,
                    Timestamp = DateTime.UtcNow
                };

                _context.OrderStatusHistories.Add(initialHistory);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        // 8. Return complete OrderResponse
        return await GetOrderByIdAsync(order.Id) 
            ?? throw new InvalidOperationException("Failed to retrieve order after creation.");
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.PickupArea!).ThenInclude(a => a.Zone)
            .Include(o => o.DropArea!).ThenInclude(a => a.Zone)
            .Include(o => o.AssignedAgent!).ThenInclude(a => a.User)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return null;

        return new OrderResponse
        {
            Id = order.Id,
            TrackingNumber = order.TrackingNumber,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.FullName ?? "Unknown Customer",

            PickupArea = order.PickupArea?.Name ?? string.Empty,
            PickupZone = order.PickupArea?.Zone?.Name ?? string.Empty,
            DropArea = order.DropArea?.Name ?? string.Empty,
            DropZone = order.DropArea?.Zone?.Name ?? string.Empty,

            PickupAddress = order.PickupAddress,
            DropAddress = order.DropAddress,

            LengthCm = order.LengthCm,
            WidthCm = order.WidthCm,
            HeightCm = order.HeightCm,

            ActualWeight = order.ActualWeightKg,
            VolumetricWeight = order.VolumetricWeightKg,
            ChargeableWeight = order.ChargeableWeightKg,

            OrderType = order.OrderType,
            PaymentType = order.PaymentType,

            RatePerKg = order.RatePerKg,
            DeliveryFee = order.DeliveryFee,
            CODSurcharge = order.CODSurcharge,
            TotalAmount = order.TotalAmount,

            Status = order.Status,
            AssignedAgentId = order.AssignedAgentId,
            AssignedAgentName = order.AssignedAgent?.User?.FullName,

            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            RescheduledDate = order.RescheduledDate,

            StatusHistory = order.StatusHistory
                .OrderBy(h => h.Timestamp)
                .Select(h => new OrderStatusHistoryDto
                {
                    Id = h.Id,
                    Status = h.Status,
                    ActorId = h.ActorId,
                    ActorRole = h.ActorRole,
                    Notes = h.Notes,
                    Timestamp = h.Timestamp
                }).ToList()
        };
    }

    public async Task<IEnumerable<OrderSummaryResponse>> GetOrdersAsync(
        int? customerId = null,
        OrderStatus? status = null,
        int? zoneId = null,
        int? agentId = null,
        string? search = null)
    {
        var query = _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.PickupArea!).ThenInclude(a => a.Zone)
            .Include(o => o.DropArea!).ThenInclude(a => a.Zone)
            .Include(o => o.AssignedAgent!).ThenInclude(a => a.User)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (zoneId.HasValue)
        {
            query = query.Where(o => (o.PickupArea != null && o.PickupArea.ZoneId == zoneId.Value) ||
                                     (o.DropArea != null && o.DropArea.ZoneId == zoneId.Value));
        }

        if (agentId.HasValue)
        {
            query = query.Where(o => o.AssignedAgentId == agentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(o => o.TrackingNumber.ToLower().Contains(searchLower) ||
                                     (o.Customer != null && o.Customer.FullName.ToLower().Contains(searchLower)) ||
                                     (o.PickupArea != null && o.PickupArea.Name.ToLower().Contains(searchLower)) ||
                                     (o.DropArea != null && o.DropArea.Name.ToLower().Contains(searchLower)) ||
                                     o.PickupAddress.ToLower().Contains(searchLower) ||
                                     o.DropAddress.ToLower().Contains(searchLower));
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(o => new OrderSummaryResponse
        {
            Id = o.Id,
            TrackingNumber = o.TrackingNumber,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.FullName ?? "Unknown Customer",
            PickupArea = o.PickupArea?.Name ?? string.Empty,
            PickupZone = o.PickupArea?.Zone?.Name ?? string.Empty,
            PickupZoneId = o.PickupArea?.ZoneId ?? 0,
            DropArea = o.DropArea?.Name ?? string.Empty,
            DropZone = orderZone(o.DropArea?.Zone?.Name),
            DropZoneId = o.DropArea?.ZoneId ?? 0,
            TotalAmount = o.TotalAmount,
            Status = o.Status,
            AssignedAgentId = o.AssignedAgentId,
            AssignedAgentName = o.AssignedAgent?.User?.FullName,
            CreatedAt = o.CreatedAt
        });
    }

    private static string orderZone(string? zoneName) => zoneName ?? string.Empty;
}
