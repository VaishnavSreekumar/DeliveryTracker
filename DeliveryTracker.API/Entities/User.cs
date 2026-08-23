using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Agent? AgentProfile { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
