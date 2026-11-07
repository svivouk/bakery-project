using Orders.Domain.Payments;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Orders.Bll.Dtos
{
    public sealed class CreatePaymentRequest
    {
        [Required]
        public Guid OrderId { get; init; }

        [EnumDataType(typeof(PaymentMethod))]
        public PaymentMethod Method { get; init; }
        [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Очікується код із трьох літер.")]
        public string Currency { get; init; } = Payment.DefaultCurrency;
    }

    public sealed class PayPaymentRequest
    {
        [StringLength(Payment.ExternalTransactionIdMaxLength)]
        public string? ExternalTransactionId { get; init; }
    }
    public sealed class RefundRequest
    {
        [Range(0.01, 9999999999.99)]
        public decimal Amount { get; init; }
    }

    public sealed class PaymentDto
    {
        public Guid Id { get; init; }
        public Guid OrderId { get; init; }
        public string Method { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public decimal RefundedAmount { get; init; }
        public decimal RefundableAmount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public string? ExternalTransactionId { get; init; }
        public DateTime? PaidAt { get; init; }
    }

}
