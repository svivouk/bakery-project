using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain;
using Orders.Domain.Messaging;
using Orders.Domain.Orders;
using Orders.Domain.Payments;
using Orders.Domain.Promotions;
using System.Text.Json;
using Orders.Dal.Repositories;

namespace Orders.Bll.Services
{
    public sealed class OrderService(IUnitOfWork uow, IMapper mapper, TimeProvider? clock = null) : IOrderService
    {
        private readonly TimeProvider _clock = clock ?? TimeProvider.System;

        public async Task<OrderDto> PlaceAsync(PlaceOrderRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var now = _clock.GetUtcNow().UtcDateTime;

            var customer = await uow.Customers.GetByIdAsync(request.CustomerId, cancellationToken)
                           ?? throw NotFoundException.For("Клієнт", request.CustomerId);
            if (customer.IsDeleted)
                throw new InvalidStateException("Клієнта анонімізовано, оформити замовлення неможливо.");

            var cart = await uow.Carts.GetForUpdateAsync(request.CustomerId, cancellationToken);
            if (cart is null || cart.Items.Count == 0)
                throw new ConflictException("Кошик порожній.");

            var lines = new List<OrderLine>(cart.Items.Count);
            foreach (var item in cart.Items)
            {
                var variant = await uow.VariantRefs.GetByIdAsync(item.VariantId, cancellationToken);
                var product = variant is null ? null : await uow.ProductRefs.GetByIdAsync(variant.ProductId, cancellationToken);
                if (variant is null || product is null || !variant.IsActive || !product.IsActive)
                    throw new ConflictException("Один із товарів у кошику більше недоступний.");
                lines.Add(new OrderLine(product.Id, variant.Id, product.Name, variant.Name, variant.Price, item.Quantity));
            }

            PromoCode? promo = null;
            if (!string.IsNullOrWhiteSpace(request.PromoCode))
            {
                var code = PromoCode.NormalizeCode(request.PromoCode);
                promo = await uow.PromoCodes.GetByCodeAsync(code, cancellationToken)
                        ?? throw NotFoundException.For("Промокод", code);
            }
            var delivery = request.DeliveryAddress is { } a
                ? Delivery.ToAddress(a.City, a.Street, a.Building, a.Apartment, a.Note)
                : Delivery.Pickup();

            var order = new Order(request.CustomerId, request.DesiredReadyAt, now, lines, delivery,
                request.DeliveryFee, request.Comment);

            if (promo is not null)
            {
                order.ApplyPromo(promo, now);
                promo.RegisterUse();
            }

            await uow.Orders.AddAsync(order, cancellationToken);   
            await uow.Payments.AddAsync(new Payment(order.Id, request.PaymentMethod, order.TotalAmount), cancellationToken);

            var payload = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                customerId = order.CustomerId,
                total = order.TotalAmount
            });
            await uow.Outbox.AddAsync(new OutboxMessage("OrderPlaced", order.Id, payload), cancellationToken);

            cart.Clear();

            await uow.SaveChangesAsync(cancellationToken);
            return mapper.Map<OrderDto>(order);
        }

        public async Task<OrderDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => mapper.Map<OrderDto>(await LoadAsync(id, cancellationToken));

        public async Task ChangeStatusAsync(Guid id, ChangeOrderStatusRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var order = await LoadAsync(id, cancellationToken);
            switch (request.Status)
            {
                case OrderStatus.Confirmed: order.Confirm(); break;
                case OrderStatus.Preparing: order.StartPreparing(); break;
                case OrderStatus.Ready: order.MarkReady(); break;
                case OrderStatus.Completed: order.Complete(); break;
                case OrderStatus.Cancelled: order.Cancel(); break;
                default: throw new InvalidStateException($"Перехід у статус {request.Status} неможливий.");
            }

            await uow.SaveChangesAsync(cancellationToken);
        }

        private async Task<Order> LoadAsync(Guid id, CancellationToken cancellationToken)
            => await uow.Orders.GetWithDetailsAsync(id, cancellationToken)
               ?? throw NotFoundException.For("Замовлення", id);
    }

}
