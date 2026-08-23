using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
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

    [HttpPost]
    [Authorize(Roles = "Customer,Admin")]
    public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // If authenticated user is a Customer, derive customerId strictly from JWT claims
        var currentUserId = GetUserId();
        if (currentUserId.HasValue && User.IsInRole("Customer"))
        {
            request.CustomerId = currentUserId.Value;
        }

        try
        {
            var result = await _orderService.CreateOrderAsync(request);
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

    [HttpGet("{id}")]
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

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<OrderSummaryResponse>>> GetOrders([FromQuery] int? customerId)
    {
        var currentUserId = GetUserId();
        if (currentUserId.HasValue)
        {
            if (User.IsInRole("Customer"))
            {
                // Customer gets only their own orders
                var customerOrders = await _orderService.GetOrdersAsync(currentUserId.Value);
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

                var allOrders = await _orderService.GetOrdersAsync(null);
                var agentOrders = allOrders.Where(o => o.AssignedAgentId == agent.Id);
                return Ok(agentOrders);
            }
        }

        // Admin (or unauthenticated unit test runner) gets requested customerId or all orders
        var orders = await _orderService.GetOrdersAsync(customerId);
        return Ok(orders);
    }

    [HttpPost("{id}/auto-assign")]
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

    [HttpPatch("{id}/status")]
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

    [HttpPost("{id}/reschedule")]
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
