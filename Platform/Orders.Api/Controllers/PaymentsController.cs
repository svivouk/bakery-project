using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Middleware;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Produces("application/json")]
    public sealed class PaymentsController(IPaymentService service) : ControllerBase
    {
        [HttpPost]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PaymentDto>> Create(CreatePaymentRequest request, CancellationToken cancellationToken)
        {
            var dto = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDto>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await service.GetAsync(id, cancellationToken));

        [HttpPost("{id:guid}/pay")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PaymentDto>> Pay(Guid id, PayPaymentRequest request, CancellationToken cancellationToken)
            => Ok(await service.PayAsync(id, request, cancellationToken));

        [HttpPost("{id:guid}/refunds")]
        [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<PaymentDto>> Refund(
            Guid id,
            RefundRequest request,
            [FromServices] IValidator<RefundRequest> validator,
            CancellationToken cancellationToken)
        {
            if (await this.ValidateOrProblemAsync(validator, request, cancellationToken) is { } problem)
                return problem;

            return Ok(await service.RefundAsync(id, request, cancellationToken));
        }
    }
}
