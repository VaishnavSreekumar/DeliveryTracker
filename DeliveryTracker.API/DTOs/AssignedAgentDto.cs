namespace DeliveryTracker.API.DTOs;

public class AssignedAgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
}
