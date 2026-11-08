using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Common.Models;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.DeleteReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Controllers;
[ApiController]
[Route("api/reviews")]
[Produces("application/json")]
public sealed class ReviewsController(ISender sender, IMapper mapper) : ControllerBase
{
    /// <summary>Створити відгук. Новий відгук має статус Pending до модерації.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var review = await sender.Send(mapper.Map<CreateReviewCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = review.Id }, review); 
    }

    /// <summary>Відгук за ідентифікатором (ObjectId).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
        => Ok(await sender.Send(new GetReviewByIdQuery(id), ct));

    /// <summary>Список відгуків: фільтри (productId, authorId, status, minRating, maxRating), sort, page, pageSize.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> List([FromQuery] GetReviewsListQuery query, CancellationToken ct)
        => Ok(await sender.Send(query, ct));

    /// <summary>Змінити оцінку й текст. Потрібна поточна версія: якщо відгук уже змінили, відповідь 409.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UpdateReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateReviewResponse>> Update(string id, UpdateReviewRequest request, CancellationToken ct)
    {
        var version = await sender.Send(
            new UpdateReviewCommand(id, request.AuthorId, request.ExpectedVersion, request.Rating, request.Text), ct);

        return Ok(new UpdateReviewResponse(version));
    }

    /// <summary>М'яко видалити відгук (автор або працівник). Виконавець передається параметрами, доки немає автентифікації.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, [FromQuery] Guid actorId, [FromQuery] bool isStaff = false, CancellationToken ct = default)
    {
        await sender.Send(new DeleteReviewCommand(id, actorId, isStaff), ct);
        return NoContent();
    }
}