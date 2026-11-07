using Orders.Domain.Orders;
using Orders.Domain.Payments;
using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Orders.Bll.Dtos;

public sealed class DeliveryAddressDto
{
    [Required, StringLength(Delivery.CityMaxLength)]
    public string City { get; init; } = string.Empty;

    [Required, StringLength(Delivery.StreetMaxLength)]
    public string Street { get; init; } = string.Empty;

    [Required, StringLength(Delivery.BuildingMaxLength)]
    public string Building { get; init; } = string.Empty;

    [StringLength(Delivery.ApartmentMaxLength)]
    public string? Apartment { get; init; }

    [StringLength(Delivery.AddressNoteMaxLength)]
    public string? Note { get; init; }
}

public sealed class PlaceOrderRequest
{
    [Required]
    public Guid CustomerId { get; init; }

    public DateTime DesiredReadyAt { get; init; }

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; init; }
    public DeliveryAddressDto? DeliveryAddress { get; init; }

    [Range(0, 9999999999.99)]
    public decimal DeliveryFee { get; init; }

    [StringLength(Order.CommentMaxLength)]
    public string? Comment { get; init; }

    public string? PromoCode { get; init; }
}

public sealed class ChangeOrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; init; }
}

public sealed class OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public Guid VariantId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string VariantName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public decimal LineTotal { get; init; }
}

public sealed class DeliveryDto
{
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? Street { get; init; }
    public string? Building { get; init; }
    public string? Apartment { get; init; }
    public string? AddressNote { get; init; }
    public string? CourierName { get; init; }
    public string? CourierPhone { get; init; }
    public DateTime? DeliveredAt { get; init; }
}

public sealed class OrderDto
{
    public Guid Id { get; init; }
    public long OrderNumber { get; init; }
    public Guid CustomerId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime DesiredReadyAt { get; init; }
    public string? Comment { get; init; }
    public Guid? PromoCodeId { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal TotalAmount { get; init; }
    public IReadOnlyList<OrderItemDto> Items { get; init; } = [];
    public DeliveryDto? Delivery { get; init; }
}

public sealed class OrderSummaryDto
{
    public Guid Id { get; init; }
    public long OrderNumber { get; init; }
    public Guid CustomerId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime DesiredReadyAt { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal TotalAmount { get; init; }
}


public sealed class OrderListQuery : PagedQuery
{
    public Guid? CustomerId { get; set; }
    public OrderStatus? Status { get; set; }
    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    [Range(0, 9999999999.99, ErrorMessage = "minTotal: значення поза допустимим діапазоном.")]
    public decimal? MinTotal { get; set; }

    [Range(0, 9999999999.99, ErrorMessage = "maxTotal: значення поза допустимим діапазоном.")]
    public decimal? MaxTotal { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "orderNumber: значення має бути додатним.")]
    public long? OrderNumber { get; set; }

    protected override IReadOnlyCollection<string> GetSortableFields() => OrderListSpecification.SortableFields;

    protected override IEnumerable<ValidationResult> ValidateFilters()
    {
        if (CreatedFrom is { } from && CreatedTo is { } to && from > to)
            yield return new ValidationResult("createdFrom не може бути пізніше за createdTo.", [nameof(CreatedFrom), nameof(CreatedTo)]);

        if (MinTotal is { } min && MaxTotal is { } max && min > max)
            yield return new ValidationResult("minTotal не може бути більшим за maxTotal.", [nameof(MinTotal), nameof(MaxTotal)]);
    }
}
