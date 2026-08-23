using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves notifications for the authenticated user.
    /// Customers and Agents can only view their own notifications.
    /// Admins can view all notifications.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications()
    {
        var (userId, userRole) = GetAuthenticatedUser();
        if (userId == null) return Unauthorized("Invalid token claims.");

        var query = _context.Notifications
            .Include(n => n.Order)
            .AsQueryable();

        if (userRole != UserRole.Admin)
        {
            query = query.Where(n => n.UserId == userId.Value);
        }

        var notifications = await query
            .OrderByDescending(n => n.SentAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                OrderId = n.OrderId,
                OrderTrackingNumber = n.Order != null ? n.Order.TrackingNumber : string.Empty,
                Title = n.Title,
                Message = n.Message,
                RecipientEmail = n.RecipientEmail,
                IsRead = n.IsRead,
                SentAt = n.SentAt
            })
            .ToListAsync();

        return Ok(notifications);
    }

    /// <summary>
    /// Marks a notification as read.
    /// Users can only mark their own notifications as read unless Admin.
    /// </summary>
    [HttpPatch("{id:int}/read")]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(int id)
    {
        var (userId, userRole) = GetAuthenticatedUser();
        if (userId == null) return Unauthorized("Invalid token claims.");

        var notification = await _context.Notifications
            .Include(n => n.Order)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (notification == null)
        {
            return NotFound($"Notification with ID {id} not found.");
        }

        if (userRole != UserRole.Admin && notification.UserId != userId.Value)
        {
            return Forbid();
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            OrderId = notification.OrderId,
            OrderTrackingNumber = notification.Order != null ? notification.Order.TrackingNumber : string.Empty,
            Title = notification.Title,
            Message = notification.Message,
            RecipientEmail = notification.RecipientEmail,
            IsRead = notification.IsRead,
            SentAt = notification.SentAt
        });
    }

    private (int? userId, UserRole? role) GetAuthenticatedUser()
    {
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

        if (int.TryParse(subClaim, out int id) && Enum.TryParse<UserRole>(roleClaim, out var role))
        {
            return (id, role);
        }

        return (null, null);
    }
}
