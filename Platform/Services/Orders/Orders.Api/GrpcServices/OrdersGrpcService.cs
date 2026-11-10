using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Grpc.Core;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Orders.Domain.Orders;
using Orders.Grpc;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;


namespace Orders.Api.GrpcServices;

public sealed class OrdersGrpcService(IOrderService orders) : OrdersGrpc.OrdersGrpcBase
{
    public override async Task<OrderInfo> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        var id = ParseGuid(request.OrderId, nameof(request.OrderId));
        return ToInfo(await orders.GetAsync(id, context.CancellationToken));
    }

    public override async Task<ListOrdersReply> ListOrders(ListOrdersRequest request, ServerCallContext context)
    {
        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw Invalid($"status: '{request.Status}' невідомий. Допустимі: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
            status = parsed;
        }

        var query = new OrderListQuery
        {
            CustomerId = request.HasCustomerId ? ParseGuid(request.CustomerId, nameof(request.CustomerId)) : null,
            Status = status,
            Page = request.Page <= 0 ? 1 : request.Page,
            PageSize = request.PageSize <= 0 ? PagedQuery.DefaultPageSize : request.PageSize,
            Sort = string.IsNullOrWhiteSpace(request.Sort) ? null : request.Sort
        };

        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, validateAllProperties: true))
            throw Invalid(string.Join("; ", errors.Select(e => e.ErrorMessage)));

        var page = await orders.ListAsync(query, context.CancellationToken);

        var reply = new ListOrdersReply { Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount };
        reply.Items.AddRange(page.Items.Select(o => new OrderSummaryInfo
        {
            OrderId = o.Id.ToString(),
            OrderNumber = o.OrderNumber,
            CustomerId = o.CustomerId.ToString(),
            Status = o.Status,
            CreatedAt = Utc(o.CreatedAt),
            DesiredReadyAt = Utc(o.DesiredReadyAt),
            Subtotal = Money(o.Subtotal),
            DiscountAmount = Money(o.DiscountAmount),
            DeliveryFee = Money(o.DeliveryFee),
            TotalAmount = Money(o.TotalAmount)
        }));
        return reply;
    }

    // ====================== перетворення ======================

    private static OrderInfo ToInfo(OrderDto o)
    {
        var info = new OrderInfo
        {
            OrderId = o.Id.ToString(),
            OrderNumber = o.OrderNumber,
            CustomerId = o.CustomerId.ToString(),
            Status = o.Status,
            DesiredReadyAt = Utc(o.DesiredReadyAt),
            Subtotal = Money(o.Subtotal),
            DiscountAmount = Money(o.DiscountAmount),
            DeliveryFee = Money(o.DeliveryFee),
            TotalAmount = Money(o.TotalAmount)
        };
        if (o.Comment is not null) info.Comment = o.Comment;
        if (o.PromoCodeId is { } promo) info.PromoCodeId = promo.ToString();

        info.Items.AddRange(o.Items.Select(i => new OrderItemInfo
        {
            ItemId = i.Id.ToString(),
            ProductId = i.ProductId.ToString(),
            VariantId = i.VariantId.ToString(),
            ProductName = i.ProductName,
            VariantName = i.VariantName,
            UnitPrice = Money(i.UnitPrice),
            Quantity = i.Quantity,
            LineTotal = Money(i.LineTotal)
        }));

        if (o.Delivery is { } d)
        {
            var delivery = new DeliveryInfo { Type = d.Type, Status = d.Status };
            if (d.City is not null) delivery.City = d.City;
            if (d.Street is not null) delivery.Street = d.Street;
            if (d.Building is not null) delivery.Building = d.Building;
            if (d.Apartment is not null) delivery.Apartment = d.Apartment;
            if (d.AddressNote is not null) delivery.AddressNote = d.AddressNote;
            if (d.CourierName is not null) delivery.CourierName = d.CourierName;
            if (d.CourierPhone is not null) delivery.CourierPhone = d.CourierPhone;
            if (d.DeliveredAt is { } at) delivery.DeliveredAt = Utc(at);
            info.Delivery = delivery;
        }

        return info;
    }

    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static Timestamp Utc(DateTime value) => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static Guid ParseGuid(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw Invalid($"{field}: '{value}' не є коректним GUID.");

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
