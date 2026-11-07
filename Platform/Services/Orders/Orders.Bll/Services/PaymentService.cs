using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Orders;
using Orders.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Services
{
    public sealed class PaymentService(IUnitOfWork uow, IMapper mapper, TimeProvider? clock = null) : IPaymentService
    {
        private readonly TimeProvider _clock = clock ?? TimeProvider.System;

        public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var order = await uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                        ?? throw NotFoundException.For("Замовлення", request.OrderId);
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
                throw new ConflictException($"Замовлення у статусі {order.Status}, оплатити його не можна.");

            var attempts = await uow.Payments.GetByOrderAsync(order.Id, cancellationToken);
            if (attempts.Any(p => p.Status is PaymentStatus.Pending or PaymentStatus.Paid or PaymentStatus.PartiallyRefunded))
                throw new ConflictException("Для цього замовлення вже є активний або сплачений платіж.");

            var payment = new Payment(order.Id, request.Method, order.TotalAmount, request.Currency);
            await uow.Payments.AddAsync(payment, cancellationToken);
            await uow.SaveChangesAsync(cancellationToken);
            return mapper.Map<PaymentDto>(payment);
        }

        public async Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => mapper.Map<PaymentDto>(await LoadAsync(id, cancellationToken));

        public async Task<PaymentDto> PayAsync(Guid id, PayPaymentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var payment = await LoadAsync(id, cancellationToken);

            if (request.ExternalTransactionId is not null)
                payment.AttachExternalTransaction(request.ExternalTransactionId);
            payment.MarkPaid(_clock.GetUtcNow().UtcDateTime);

            await uow.SaveChangesAsync(cancellationToken);
            return mapper.Map<PaymentDto>(payment);
        }

        public async Task<PaymentDto> RefundAsync(Guid id, RefundRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var payment = await LoadAsync(id, cancellationToken);

            payment.Refund(request.Amount);

            await uow.SaveChangesAsync(cancellationToken);
            return mapper.Map<PaymentDto>(payment);
        }

        private async Task<Payment> LoadAsync(Guid id, CancellationToken cancellationToken)
            => await uow.Payments.GetByIdAsync(id, cancellationToken)
               ?? throw NotFoundException.For("Платіж", id);
    }

}
