namespace DeliveryTracker.API.Entities;

public class Agent
{
    public int Id { get; set; }
    
    public int UserId { get; set; }
    public User? User { get; set; }

    public int ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public bool IsAvailable { get; set; } = true;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public ICollection<Order> AssignedOrders { get; set; } = new List<Order>();
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; set; } = new List<DeliveryAttempt>();
}
