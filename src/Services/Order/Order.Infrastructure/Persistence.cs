using System.Text.Json;
using BuildingBlocks.Contracts;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Application;
using Order.Domain;

namespace Order.Infrastructure;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredOn { get; init; }
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset? ProcessedOn { get; set; }
    public string? Error { get; set; }
}

public sealed class InboxMessage
{
    public Guid MessageId { get; init; }
    public required string Consumer { get; init; }
    public DateTimeOffset ProcessedOn { get; init; }
}

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options), IOrderUnitOfWork
{
    public DbSet<Domain.Order> Orders => Set<Domain.Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.Entity<OutboxMessage>(b => { b.ToTable("OutboxMessages"); b.HasKey(x => x.Id); b.Property(x => x.Payload).HasColumnType("nvarchar(max)"); b.HasIndex(x => x.ProcessedOn); });
        modelBuilder.Entity<InboxMessage>(b => { b.ToTable("InboxMessages"); b.HasKey(x => new { x.MessageId, x.Consumer }); b.Property(x => x.Consumer).HasMaxLength(200); });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>().Select(x => x.Entity).Where(x => x.DomainEvents.Count != 0).ToArray();
        foreach (var aggregate in aggregates)
        foreach (var domainEvent in aggregate.DomainEvents)
            OutboxMessages.Add(new OutboxMessage
            {
                Id = domainEvent.EventId, OccurredOn = domainEvent.OccurredOn,
                Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
            });
        var affectedRows = await base.SaveChangesAsync(cancellationToken);
        foreach (var aggregate in aggregates) aggregate.ClearDomainEvents();
        return affectedRows;
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Domain.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Order> builder)
    {
        builder.ToTable("Orders"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasConversion(x => x.Value, x => new OrderId(x));
        builder.Property(x => x.CustomerId).HasMaxLength(100).IsRequired(); builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40); builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Ignore(x => x.DomainEvents); builder.Ignore(x => x.Total);
        builder.OwnsMany(x => x.Items, item =>
        {
            item.ToTable("OrderItems"); item.WithOwner().HasForeignKey("OrderId"); item.HasKey(x => x.Id);
            item.Property(x => x.ProductId).HasMaxLength(100); item.Property(x => x.ProductName).HasMaxLength(300);
            item.OwnsOne(x => x.UnitPrice, money => { money.Property(x => x.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2); money.Property(x => x.Currency).HasColumnName("Currency").HasMaxLength(3); });
            item.Ignore(x => x.LineTotal);
        });
    }
}

public sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task AddAsync(Domain.Order order, CancellationToken cancellationToken) => dbContext.Orders.AddAsync(order, cancellationToken).AsTask();
    public Task<Domain.Order?> GetAsync(OrderId id, CancellationToken cancellationToken) => dbContext.Orders.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> ExistsAsync(OrderId id, CancellationToken cancellationToken) => dbContext.Orders.AnyAsync(x => x.Id == id, cancellationToken);
}

public interface IIntegrationEventPublisher { Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken); }
