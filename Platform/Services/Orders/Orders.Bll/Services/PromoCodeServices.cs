using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Repositories.Interfaces;
using Orders.Domain;
using Orders.Domain.Promotions;
using Orders.Domain.Specification;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Bll.Services
{
    public sealed class PromoCodeService(IUnitOfWork uow, IMapper mapper, ILogger<PromoCodeService> logger) : IPromoCodeService
    {
        public async Task<PromoCodeDto> CreateAsync(CreatePromoCodeRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var promo = new PromoCode(request.Code, request.DiscountType, request.DiscountValue,
                request.ValidFrom, request.ValidTo, request.MinOrderAmount, request.MaxUses);

            if (await uow.PromoCodes.ExistsByCodeAsync(promo.Code, cancellationToken))
                throw new ConflictException($"Промокод '{promo.Code}' уже існує.");

            await uow.PromoCodes.AddAsync(promo, cancellationToken);
            await uow.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Promo code {PromoCode} created, discount {DiscountType} {DiscountValue}, valid from {ValidFrom} to {ValidTo}",
                promo.Code, promo.DiscountType, promo.DiscountValue, promo.ValidFrom, promo.ValidTo);
            return mapper.Map<PromoCodeDto>(promo);
        }

        public async Task<PromoCodeDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => mapper.Map<PromoCodeDto>(await LoadAsync(id, cancellationToken));

        public async Task<PagedResult<PromoCodeDto>> ListAsync(PromoCodeListQuery query, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageSize = query.GetPageSize();
            var spec = new PromoCodeListSpecification(query.Search, query.IsActive, query.DiscountType, query.ValidOn,
                query.ResolveSort(), query.Page, pageSize);

            var total = await uow.PromoCodes.CountAsync(spec, cancellationToken);
            var items = total == 0
                ? []
                : await uow.PromoCodes.ListAsync(spec, cancellationToken);

            return new PagedResult<PromoCodeDto>(
                mapper.Map<IReadOnlyList<PromoCodeDto>>(items), query.Page, pageSize, total);
        }

        public async Task<PromoCodeDto> ChangeDiscountAsync(Guid id, ChangeDiscountRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var promo = await LoadAsync(id, cancellationToken);

            // Клієнт мав бачити актуальну версію, інакше його зміна перезаписала б чужу.
            if (!promo.RowVersion.AsSpan().SequenceEqual(ParseRowVersion(request.RowVersion)))
                throw new ConcurrencyException("Промокод змінено іншим користувачем. Перечитайте дані й повторіть.");

            promo.ChangeDiscount(request.DiscountType, request.DiscountValue);
            uow.PromoCodes.Update(promo);
            await uow.SaveChangesAsync(cancellationToken);
            return mapper.Map<PromoCodeDto>(promo);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var promo = await LoadAsync(id, cancellationToken);
            promo.Delete();
            await uow.SaveChangesAsync(cancellationToken);
        }

        private async Task<PromoCode> LoadAsync(Guid id, CancellationToken cancellationToken)
            => await uow.PromoCodes.GetWithUsageAsync(id, cancellationToken)
               ?? throw NotFoundException.For("Промокод", id);

        private static byte[] ParseRowVersion(string value)
        {
            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                throw new DomainException("RowVersion: очікується рядок у форматі base64.");
            }
        }
    }
}
