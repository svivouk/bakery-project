using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api/promo-codes")]
    [Produces("application/json")]
    public sealed class PromoCodesController(IPromoCodeService service): ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(PromoCodeDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PromoCodeDto>> Create(CreatePromoCodeRequest request, CancellationToken cancellationToken)
        {
            var dto = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
       
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PromoCodeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PromoCodeDto>> GetById(Guid id, CancellationToken cancellationToken)
           => Ok(await service.GetAsync(id, cancellationToken));

        [HttpPut("{id:guid}/discount")]
        [ProducesResponseType(typeof(PromoCodeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PromoCodeDto>> ChangeDiscount(Guid id, ChangeDiscountRequest request,
       CancellationToken cancellationToken)
           => Ok(await service.ChangeDiscountAsync(id, request, cancellationToken));

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
