namespace DeliveryTracker.API.Entities;

public class Area
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public int ZoneId { get; set; }
    public Zone? Zone { get; set; }
}
