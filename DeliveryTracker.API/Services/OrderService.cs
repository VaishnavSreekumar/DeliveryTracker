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

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
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

        // 3. Reuse PricingService for exact price calculation (No calculation logic duplication!)
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

        // 6. Execute atomic database transaction for Order + initial OrderStatusHistory
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
                    ActorId = request.CustomerId,
                    ActorRole = UserRole.Customer,
                    Notes = "Order created",
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

        // 7. Return complete OrderResponse
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

    public async Task<IEnumerable<OrderSummaryResponse>> GetOrdersAsync(int? customerId = null)
    {
        var query = _context.Orders
            .Include(o => o.PickupArea)
            .Include(o => o.DropArea)
            .Include(o => o.AssignedAgent!).ThenInclude(a => a.User)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return orders.Select(o => new OrderSummaryResponse
        {
            Id = o.Id,
            TrackingNumber = o.TrackingNumber,
            CustomerId = o.CustomerId,
            PickupArea = o.PickupArea?.Name ?? string.Empty,
            DropArea = o.DropArea?.Name ?? string.Empty,
            TotalAmount = o.TotalAmount,
            Status = o.Status,
            AssignedAgentId = o.AssignedAgentId,
            AssignedAgentName = o.AssignedAgent?.User?.FullName,
            CreatedAt = o.CreatedAt
        });
    }
}
