using Orders.Bll.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Services;

public interface IPromoCodeService
{
    /// <exception cref="Common.ConflictException">Промокод із таким кодом уже існує.</exception>
    Task<PromoCodeDto> CreateAsync(CreatePromoCodeRequest request, CancellationToken cancellationToken = default);

    Task<PromoCodeDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Сторінка промокодів за фільтрами й сортуванням (усе виконується в БД).</summary>
    Task<PagedResult<PromoCodeDto>> ListAsync(PromoCodeListQuery query, CancellationToken cancellationToken = default);

    Task<PromoCodeDto> ChangeDiscountAsync(Guid id, ChangeDiscountRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaymentDto> PayAsync(Guid id, PayPaymentRequest request, CancellationToken cancellationToken = default);

    Task<PaymentDto> RefundAsync(Guid id, RefundRequest request, CancellationToken cancellationToken = default);
}

public interface IOrderService
{
    Task<OrderDto> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Сторінка замовлень (без позицій) за фільтрами й сортуванням (усе виконується в БД).</summary>
    Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken cancellationToken = default);

    Task ChangeStatusAsync(Guid id, ChangeOrderStatusRequest request, CancellationToken cancellationToken = default);
}
