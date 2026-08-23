namespace DeliveryTracker.API.Entities;

public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public ICollection<Area> Areas { get; set; } = new List<Area>();
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
}
