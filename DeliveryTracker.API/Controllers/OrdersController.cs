using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly IOrderStatusService _orderStatusService;
    private readonly IDeliveryRecoveryService _deliveryRecoveryService;
    private readonly AppDbContext _context;

    public OrdersController(
        IOrderService orderService,
        IAgentAssignmentService agentAssignmentService,
        IOrderStatusService orderStatusService,
        IDeliveryRecoveryService deliveryRecoveryService,
        AppDbContext context)
    {
        _orderService = orderService;
        _agentAssignmentService = agentAssignmentService;
        _orderStatusService = orderStatusService;
        _deliveryRecoveryService = deliveryRecoveryService;
        _context = context;
    }

    /// <summary>
    /// Creates a shipment order. Customers create for themselves; Admins can create on behalf of any Customer.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer,Admin")]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetUserId();
        UserRole creatorRole = User.IsInRole("Admin") ? UserRole.Admin : UserRole.Customer;

        // If authenticated user is a Customer, force customerId strictly from JWT claims
        if (currentUserId.HasValue && creatorRole == UserRole.Customer)
        {
            request.CustomerId = currentUserId.Value;
        }

        try
        {
            var result = await _orderService.CreateOrderAsync(request, currentUserId, creatorRole);
            return CreatedAtAction(nameof(GetOrderById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves a single order by ID with privacy scoping.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<OrderResponse>> GetOrderById(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);

        if (order == null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        var currentUserId = GetUserId();
        if (currentUserId.HasValue)
        {
            // Customer can only view their own order
            if (User.IsInRole("Customer") && order.CustomerId != currentUserId.Value)
            {
                return StatusCode(403, new { message = "You are not authorized to view another customer's order." });
            }

            // Agent can only view order assigned to them
            if (User.IsInRole("Agent"))
            {
                var agent = await _context.Agents.FirstOrDefaultAsync(a => a.UserId == currentUserId.Value);
                if (agent == null || order.AssignedAgentId != agent.Id)
                {
                    return StatusCode(403, new { message = "You are not authorized to view orders not assigned to you." });
                }
            }
        }

        return Ok(order);
    }

    /// <summary>
    /// Retrieves orders with role-scoping and multi-parameter filtering (status, zone, agent, search).
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderSummaryResponse>>> GetOrders(
        [FromQuery] int? customerId = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int? zoneId = null,
        [FromQuery] int? agentId = null,
        [FromQuery] string? search = null)
    {
        var currentUserId = GetUserId();
        if (currentUserId.HasValue)
        {
            if (User.IsInRole("Customer"))
            {
                // Customer gets only their own orders
                var customerOrders = await _orderService.GetOrdersAsync(currentUserId.Value, status, zoneId, null, search);
                return Ok(customerOrders);
            }

            if (User.IsInRole("Agent"))
            {
                // Agent gets only assigned orders
                var agent = await _context.Agents.FirstOrDefaultAsync(a => a.UserId == currentUserId.Value);
                if (agent == null)
                {
                    return Ok(Enumerable.Empty<OrderSummaryResponse>());
                }

                var agentOrders = await _orderService.GetOrdersAsync(null, status, zoneId, agent.Id, search);
                return Ok(agentOrders);
            }
        }

        // Admin gets all orders with full multi-dimensional filtering
        var orders = await _orderService.GetOrdersAsync(customerId, status, zoneId, agentId, search);
        return Ok(orders);
    }

    /// <summary>
    /// Retrieves all registered customer accounts (Admin only).
    /// </summary>
    [HttpGet("customers")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminCustomerDto>>> GetCustomers()
    {
        var customers = await _context.Users
            .Where(u => u.Role == UserRole.Customer)
            .OrderBy(u => u.FullName)
            .Select(u => new AdminCustomerDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email
            })
            .ToListAsync();

        return Ok(customers);
    }

    /// <summary>
    /// Triggers intelligent auto-assignment algorithm (Admin only).
    /// </summary>
    [HttpPost("{id:int}/auto-assign")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AgentAssignmentResponse>> AutoAssignAgent(int id)
    {
        try
        {
            var response = await _agentAssignmentService.AutoAssignAgentAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Manually assigns a specific agent to an order (Admin only).
    /// </summary>
    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AgentAssignmentResponse>> ManualAssignAgent(int id, [FromBody] ManualAssignAgentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetUserId() ?? 1;

        try
        {
            var response = await _agentAssignmentService.ManualAssignAgentAsync(id, request.AgentId, currentUserId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Standard Agent order progression through strict linear state machine.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Agent,Admin")]
    public async Task<ActionResult<OrderStatusUpdateResponse>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetUserId();
        if (currentUserId.HasValue)
        {
            request.ActorId = currentUserId.Value; // Derive actorId strictly from JWT
        }

        try
        {
            var response = await _orderStatusService.UpdateOrderStatusAsync(id, request);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Privileged Admin Status Override.
    /// Allows setting any target status with a mandatory recorded reason (Admin only).
    /// </summary>
    [HttpPost("{id:int}/override-status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<OrderStatusUpdateResponse>> OverrideOrderStatus(int id, [FromBody] AdminOverrideStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetUserId() ?? 1;

        try
        {
            var response = await _orderStatusService.OverrideOrderStatusAsync(id, request, currentUserId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reschedules a failed delivery (Customer or Admin).
    /// </summary>
    [HttpPost("{id:int}/reschedule")]
    [Authorize(Roles = "Customer,Admin")]
    public async Task<ActionResult<RescheduleOrderResponse>> RescheduleOrder(int id, [FromBody] RescheduleOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUserId = GetUserId();
        if (currentUserId.HasValue && User.IsInRole("Customer"))
        {
            request.CustomerId = currentUserId.Value; // Derive customerId strictly from JWT
        }

        try
        {
            var response = await _deliveryRecoveryService.RescheduleOrderAsync(id, request);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim != null && int.TryParse(claim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }
}
