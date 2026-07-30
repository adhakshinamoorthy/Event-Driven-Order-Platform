using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Application;
using Order.Domain;

namespace Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderDb") ?? throw new InvalidOperationException("ConnectionStrings:OrderDb is required.");
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderUnitOfWork>(sp => sp.GetRequiredService<OrderDbContext>());
        return services;
    }
}
