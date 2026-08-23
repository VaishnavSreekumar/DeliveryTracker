using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enum String Conversions
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<RateCard>()
            .Property(r => r.OrderType)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderType)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.PaymentType)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<OrderStatusHistory>()
            .Property(h => h.Status)
            .HasConversion<string>();

        modelBuilder.Entity<OrderStatusHistory>()
            .Property(h => h.ActorRole)
            .HasConversion<string>();

        // Relationships
        // User - Agent 1:1
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.User)
            .WithOne(u => u.AgentProfile)
            .HasForeignKey<Agent>(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Agent - Zone
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.Zone)
            .WithMany(z => z.Agents)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        // Area - Zone
        modelBuilder.Entity<Area>()
            .HasOne(a => a.Zone)
            .WithMany(z => z.Areas)
            .HasForeignKey(a => a.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        // Order - Customer
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order - PickupArea
        modelBuilder.Entity<Order>()
            .HasOne(o => o.PickupArea)
            .WithMany()
            .HasForeignKey(o => o.PickupAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order - DropArea
        modelBuilder.Entity<Order>()
            .HasOne(o => o.DropArea)
            .WithMany()
            .HasForeignKey(o => o.DropAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order - AssignedAgent
        modelBuilder.Entity<Order>()
            .HasOne(o => o.AssignedAgent)
            .WithMany(a => a.AssignedOrders)
            .HasForeignKey(o => o.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        // OrderStatusHistory - Order
        modelBuilder.Entity<OrderStatusHistory>()
            .HasOne(h => h.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // DeliveryAttempt - Order & Agent
        modelBuilder.Entity<DeliveryAttempt>()
            .HasOne(d => d.Order)
            .WithMany(o => o.DeliveryAttempts)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryAttempt>()
            .HasOne(d => d.Agent)
            .WithMany(a => a.DeliveryAttempts)
            .HasForeignKey(d => d.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification - Order
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Order)
            .WithMany(o => o.Notifications)
            .HasForeignKey(n => n.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Notification - User
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
