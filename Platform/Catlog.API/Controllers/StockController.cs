using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/stock")]
public sealed class StockController : ControllerBase
{
    private readonly IStockService _stock;

    public StockController(IStockService stock) => _stock = stock;

    [HttpGet("{variantId:guid}")]
    public async Task<ActionResult<StockDto>> Get(Guid variantId, CancellationToken ct) =>
        Ok(await _stock.GetAsync(variantId, ct));
    [HttpPut("{variantId:guid}")]
    public async Task<ActionResult<StockDto>> Adjust(Guid variantId, AdjustStockRequest request, CancellationToken ct) =>
        Ok(await _stock.AdjustAsync(variantId, request, ct));

    /// <summary>Атомарно резервує всі позиції замовлення (все або нічого). 409, якщо залишку не вистачає.</summary>
    [HttpPost("reservations")]
    public async Task<ActionResult<ReservationDto>> Reserve(ReserveRequest request, CancellationToken ct) =>
        Ok(await _stock.ReserveAsync(request, ct));

    [HttpPost("reservations/{orderId:guid}/commit")]
    public async Task<IActionResult> Commit(Guid orderId, CancellationToken ct)
    {
        await _stock.CommitAsync(orderId, ct);
        return NoContent();
    }

    [HttpDelete("reservations/{orderId:guid}")]
    public async Task<IActionResult> Release(Guid orderId, CancellationToken ct) =>
        Ok(new { released = await _stock.ReleaseAsync(orderId, ct) });

    // ---------- обслуговування ----------

    [HttpPost("maintenance/release-expired")]
    public async Task<IActionResult> ReleaseExpired(CancellationToken ct) =>
        Ok(new { released = await _stock.ReleaseExpiredAsync(ct) });

    ///Скидання добової норми. Запускати ззовні раз на добу (cron / SQL Agent / CronJob).
    [HttpPost("maintenance/reset-daily")]
    public async Task<IActionResult> ResetDaily(CancellationToken ct) =>
        Ok(new { changed = await _stock.ResetDailyAsync(ct) });
}
