namespace DeliveryTracker.API.Entities;

public class DeliveryAttempt
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int AgentId { get; set; }
    public Agent? Agent { get; set; }

    public int AttemptNumber { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public DateTime? RescheduledDate { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
