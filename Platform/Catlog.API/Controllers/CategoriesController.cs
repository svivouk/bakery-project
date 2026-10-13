using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(
        [FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(await _categories.ListAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _categories.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        var created = await _categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct) =>
        Ok(await _categories.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
