using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain.Orders;
using Orders.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Services
{
    public sealed class PaymentService(IUnitOfWork uow, IMapper mapper, ILogger<PaymentService> logger, TimeProvider? clock = null) : IPaymentService
    {
        private readonly TimeProvider _clock = clock ?? TimeProvider.System;

        public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            using var activity = Telemetry.Source.StartActivity("CreatePayment");
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

            activity?.SetTag("payment.id", payment.Id);
            activity?.SetTag("payment.items", payment.OrderId);
            logger.LogInformation("Payment {PaymentId} created for order {OrderId}, amount {Amount}, status {Status}",
                                     payment.Id, payment.OrderId, payment.Amount, payment.Status);
            return mapper.Map<PaymentDto>(payment);
        }

        public async Task<PaymentDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => mapper.Map<PaymentDto>(await LoadAsync(id, cancellationToken));

        public async Task<PaymentDto> PayAsync(Guid id, PayPaymentRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            using var activity = Telemetry.Source.StartActivity("PayPayment");
            var payment = await LoadAsync(id, cancellationToken);

            if (request.ExternalTransactionId is not null)
                payment.AttachExternalTransaction(request.ExternalTransactionId);
            payment.MarkPaid(_clock.GetUtcNow().UtcDateTime);

            await uow.SaveChangesAsync(cancellationToken);
            activity?.SetTag("payment.id", payment.Id);
            activity?.SetTag("payment.status", payment.Status);
            activity?.SetTag("payment.amount", payment.Amount);
            logger.LogInformation("Payment {PaymentId} paid, amount {Amount}, status {Status}", payment.Id, payment.Amount, payment.Status);
            return mapper.Map<PaymentDto>(payment);
        }

        public async Task<PaymentDto> RefundAsync(Guid id, RefundRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var payment = await LoadAsync(id, cancellationToken);

            payment.Refund(request.Amount);

            await uow.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Payment {PaymentId} refunded {RefundAmount}, status {Status}", payment.Id, request.Amount, payment.Status);
            return mapper.Map<PaymentDto>(payment);
        }

        private async Task<Payment> LoadAsync(Guid id, CancellationToken cancellationToken)
            => await uow.Payments.GetByIdAsync(id, cancellationToken)
               ?? throw NotFoundException.For("Платіж", id);
    }

}
