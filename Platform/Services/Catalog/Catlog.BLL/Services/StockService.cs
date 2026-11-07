using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Constants;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;

namespace Catalog.Bll.Services;

public sealed class StockService : IStockService
{
    private const int DefaultTtlMinutes = 15;
    private const int MaxTtlMinutes = 24 * 60;
    private const int MaxLines = 50;
    private const int ExpiredBatchSize = 500;

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public StockService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<StockDto> GetAsync(Guid variantId, CancellationToken ct = default)
    {
        var item = await _uow.Stock.GetByIdAsync(variantId, ct)
                   ?? throw new NotFoundException("StockItem", variantId);
        return _mapper.Map<StockDto>(item);
    }

    public async Task<StockDto> AdjustAsync(Guid variantId, AdjustStockRequest request, CancellationToken ct = default)
    {
        var quantity = Check.NonNegative(request.QuantityAvailable, "quantityAvailable");
        var dailyLimit = Check.NonNegative(request.DailyLimit, "dailyLimit");

        await _uow.InTransactionAsync(async () =>
        {
            var variant = await _uow.Variants.GetByIdAsync(variantId, ct)
                          ?? throw new NotFoundException("Variant", variantId);

            var updated = await _uow.Stock.UpdateAsync(new StockItem
            {
                VariantId = variantId,
                QuantityAvailable = quantity,
                DailyLimit = dailyLimit
            }, ct);
            if (!updated) throw new NotFoundException("StockItem", variantId);

            await EventPublisher.PublishAsync(_uow, EventTypes.StockChanged, variant.ProductId, new
            {
                productId = variant.ProductId,
                variantId,
                quantityAvailable = quantity,
                reason = "adjust"
            }, ct);
        }, ct);

        return await GetAsync(variantId, ct);
    }

    public async Task<ReservationDto> ReserveAsync(ReserveRequest request, CancellationToken ct = default)
    {
        if (request.OrderId == Guid.Empty)
            throw new ValidationException("orderId обов'язковий.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new ValidationException("Потрібна принаймні одна позиція.");
        if (request.Lines.Count > MaxLines)
            throw new ValidationException($"Забагато позицій (максимум {MaxLines}).");
        if (request.Lines.Any(l => l.Quantity <= 0))
            throw new ValidationException("Кількість у кожній позиції має бути більшою за нуль.");
        if (request.Lines.GroupBy(l => l.VariantId).Any(g => g.Count() > 1))
            throw new ValidationException("Один варіант не може повторюватися в позиціях.");

        var ttl = Math.Clamp(request.TtlMinutes ?? DefaultTtlMinutes, 1, MaxTtlMinutes);
        var expiresAt = DateTime.UtcNow.AddMinutes(ttl);

        // Все або нічого: якщо будь-яка позиція не пройшла, відкочуються й попередні.
        // Фіксований порядок блокувань (за VariantId) зменшує ризик дедлоків між паралельними замовленнями.
        await _uow.InTransactionAsync(async () =>
        {
            foreach (var line in request.Lines.OrderBy(l => l.VariantId))
                await _uow.Stock.ReserveAsync(request.OrderId, line.VariantId, line.Quantity, expiresAt, ct);
        }, ct);

        return new ReservationDto(request.OrderId, expiresAt, request.Lines);
    }

    public Task CommitAsync(Guid orderId, CancellationToken ct = default) =>
        _uow.InTransactionAsync(() => _uow.Stock.CommitAsync(orderId, ct), ct);

    public Task<int> ReleaseAsync(Guid orderId, CancellationToken ct = default) =>
        _uow.InTransactionAsync(() => _uow.Stock.ReleaseAsync(orderId, ct), ct);

    public async Task<int> ReleaseExpiredAsync(CancellationToken ct = default)
    {
        var total = 0;
        int released;
        do
        {
            released = await _uow.InTransactionAsync(
                () => _uow.Stock.ReleaseExpiredAsync(ExpiredBatchSize, ct), ct);
            total += released;
        } while (released >= ExpiredBatchSize && !ct.IsCancellationRequested);

        return total;
    }

    public Task<int> ResetDailyAsync(CancellationToken ct = default) =>
        _uow.InTransactionAsync(() => _uow.Stock.ResetDailyAsync(ct), ct);
}
