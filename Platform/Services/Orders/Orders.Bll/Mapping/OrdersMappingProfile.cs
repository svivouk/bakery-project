using Orders.Bll.Dtos;
using Orders.Domain.Orders;
using Orders.Domain.Payments;
using Orders.Domain.Promotions;
using AutoMapper;

namespace Orders.Bll.Mapping
{
    public sealed class OrdersMappingProfile : Profile
    {
        public OrdersMappingProfile()
        {
            CreateMap<PromoCode, PromoCodeDto>()
          .ForMember(d => d.UsedCount, o => o.MapFrom(s => s.Usage.UsedCount))
          .ForMember(d => d.RowVersion, o => o.MapFrom(s => Convert.ToBase64String(s.RowVersion)));

            CreateMap<Payment, PaymentDto>();

            CreateMap<OrderItem, OrderItemDto>();
            CreateMap<Delivery, DeliveryDto>();
            CreateMap<Order, OrderDto>();
            CreateMap<Order, OrderSummaryDto>();
        }
    }

}
