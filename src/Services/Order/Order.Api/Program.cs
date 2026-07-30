using System.Diagnostics;
using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Order.Application;
using Order.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration.ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext().WriteTo.Console());
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddHealthChecks();
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();
    builder.Services.AddMediatR(config => { config.RegisterServicesFromAssemblyContaining<CreateOrderCommand>(); config.AddOpenBehavior(typeof(LoggingBehavior<,>)); });
    builder.Services.AddOrderInfrastructure(builder.Configuration);
    var app = builder.Build();
    app.UseExceptionHandler(); app.UseSerilogRequestLogging(); app.UseHttpsRedirection();
    app.MapHealthChecks("/health"); app.MapOpenApi(); app.UseSwagger(); app.UseSwaggerUI();
    var orders = app.MapGroup("/api/orders").WithTags("Orders");
    orders.MapPost("/", async (CreateOrderRequest request, ISender sender, CancellationToken ct) =>
    {
        var id = await sender.Send(new CreateOrderCommand(request.CustomerId, request.Currency, request.Items.Select(x => new CreateOrderLine(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice)).ToArray()), ct);
        return Results.Created($"/api/orders/{id}", new { id });
    }).Produces(StatusCodes.Status201Created).ProducesValidationProblem();
    orders.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetOrderQuery(id), ct))).Produces<OrderDto>().ProducesProblem(StatusCodes.Status404NotFound);
    orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest request, ISender sender, CancellationToken ct) => { await sender.Send(new CancelOrderCommand(id, request.Reason), ct); return Results.NoContent(); }).Produces(StatusCodes.Status204NoContent);
    app.Run();
}
catch (Exception exception) { Log.Fatal(exception, "Order API terminated unexpectedly"); }
finally { await Log.CloseAndFlushAsync(); }

public sealed record CreateOrderItemRequest(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public sealed record CreateOrderRequest(string CustomerId, string Currency, IReadOnlyCollection<CreateOrderItemRequest> Items);
public sealed record CancelOrderRequest(string Reason);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch { NotFoundException => (404, "Resource not found"), DomainException => (422, "Business rule violated"), _ => (500, "An unexpected error occurred") };
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, Exception = exception, ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = status == 500 ? null : exception.Message } });
    }
}

public partial class Program;
