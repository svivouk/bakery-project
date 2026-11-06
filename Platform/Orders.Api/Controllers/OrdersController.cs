using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Middleware;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Produces("application/json")]
    public sealed class OrdersController(IOrderService service) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<OrderDto>> Place(
            PlaceOrderRequest request,
            [FromServices] IValidator<PlaceOrderRequest> validator,
            CancellationToken cancellationToken)
        {
            if (await this.ValidateOrProblemAsync(validator, request, cancellationToken) is { } problem)
                return problem;

            var dto = await service.PlaceAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        /// <summary>Список замовлень: фільтри, сортування (sort=-createdAt або sortBy/sortDir), page/pageSize.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<OrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<OrderSummaryDto>>> List([FromQuery] OrderListQuery query, CancellationToken cancellationToken)
            => Ok(await service.ListAsync(query, cancellationToken));

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await service.GetAsync(id, cancellationToken));

        [HttpPut("{id:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ChangeStatus(
            Guid id,
            ChangeOrderStatusRequest request,
            [FromServices] IValidator<ChangeOrderStatusRequest> validator,
            CancellationToken cancellationToken)
        {
            if (await this.ValidateOrProblemAsync(validator, request, cancellationToken) is { } problem)
                return problem;

            await service.ChangeStatusAsync(id, request, cancellationToken);
            return NoContent();
        }
    }
}
