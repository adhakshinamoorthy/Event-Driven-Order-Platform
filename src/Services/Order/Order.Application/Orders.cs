using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;
using Order.Domain;

namespace Order.Application;

public sealed record CreateOrderLine(string ProductId, string ProductName, int Quantity, decimal UnitPrice);
public sealed record CreateOrderCommand(string CustomerId, string Currency, IReadOnlyCollection<CreateOrderLine> Items) : IRequest<Guid>;
public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest;
public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto>;
public sealed record OrderLineDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Total);
public sealed record OrderDto(Guid Id, string CustomerId, string Status, decimal Total, string Currency, DateTimeOffset CreatedOn, IReadOnlyCollection<OrderLineDto> Items);

public interface IOrderUnitOfWork { Task<int> SaveChangesAsync(CancellationToken cancellationToken); }

public sealed class CreateOrderHandler(IOrderRepository repository, IOrderUnitOfWork unitOfWork) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = Domain.Order.Create(request.CustomerId, request.Currency,
            request.Items.Select(x => (x.ProductId, x.ProductName, x.Quantity, x.UnitPrice)));
        await repository.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id.Value;
    }
}

public sealed class CancelOrderHandler(IOrderRepository repository, IOrderUnitOfWork unitOfWork) : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(new OrderId(request.OrderId), cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);
        order.Cancel(request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetOrderHandler(IOrderRepository repository) : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.GetAsync(new OrderId(request.OrderId), cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);
        return new(order.Id.Value, order.CustomerId, order.Status.ToString(), order.Total, order.Currency, order.CreatedOn,
            order.Items.Select(x => new OrderLineDto(x.ProductId, x.ProductName, x.Quantity, x.UnitPrice.Amount, x.LineTotal)).ToArray());
    }
}

public sealed class LoggingBehavior<TRequest, TResponse>(Microsoft.Extensions.Logging.ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next(cancellationToken);
        logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}
