using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    // ---------- вітрина ----------

    /// Список товарів: фільтри, сортування, пагінація (vw_ProductListing).
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<ProductListItemDto>>> List(
        [FromQuery] ListingQuery query, CancellationToken ct) =>
        Ok(await _products.GetListingAsync(query, ct));

    [HttpGet("top-rated")]
    public async Task<ActionResult<IReadOnlyList<ProductListItemDto>>> TopRated(
        [FromQuery] int count = 10, CancellationToken ct = default) =>
        Ok(await _products.GetTopRatedAsync(count, ct));

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<ProductDetailsDto>> GetBySlug(string slug, CancellationToken ct) =>
        Ok(await _products.GetDetailsBySlugAsync(slug, ct));

    // ---------- адмін-частина ----------

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _products.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await _products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.ProductId }, created);
    }

    ///Оновлення з оптимістичною конкурентністю: у тілі потрібен rowVersion, отриманий при читанні.
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request, CancellationToken ct) =>
        Ok(await _products.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---------- варіанти ----------

    [HttpPost("{id:guid}/variants")]
    public async Task<ActionResult<VariantDto>> AddVariant(Guid id, CreateVariantRequest request, CancellationToken ct)
    {
        var created = await _products.AddVariantAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    [HttpPut("{id:guid}/variants/{variantId:guid}")]
    public async Task<ActionResult<VariantDto>> UpdateVariant(
        Guid id, Guid variantId, UpdateVariantRequest request, CancellationToken ct) =>
        Ok(await _products.UpdateVariantAsync(id, variantId, request, ct));

    [HttpDelete("{id:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid id, Guid variantId, CancellationToken ct)
    {
        await _products.DeleteVariantAsync(id, variantId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api")]
public sealed class ReferenceController : ControllerBase
{
    private readonly IProductService _products;

    public ReferenceController(IProductService products) => _products = products;

    [HttpGet("ingredients")]
    public async Task<ActionResult<IReadOnlyList<IngredientDto>>> Ingredients(CancellationToken ct) =>
        Ok(await _products.GetIngredientsAsync(ct));

    [HttpGet("allergens")]
    public async Task<ActionResult<IReadOnlyList<AllergenRefDto>>> Allergens(CancellationToken ct) =>
        Ok(await _products.GetAllergensAsync(ct));
}

/// <summary>
/// HTTP-вхід для Inbox: імітує споживача RatingSummaryChanged з контексту «Відгуки».
/// У реальній системі це був би обробник повідомлень брокера, що викликає той самий сервіс.
/// </summary>
[ApiController]
[Route("api/events")]
public sealed class InboxController : ControllerBase
{
    private readonly IProductService _products;

    public InboxController(IProductService products) => _products = products;

    [HttpPost("rating-summary-changed")]
    public async Task<IActionResult> RatingSummaryChanged(RatingSummaryChangedEvent e, CancellationToken ct)
    {
        var applied = await _products.ApplyRatingSummaryAsync(e, ct);
        return Ok(new { applied });
    }
}
