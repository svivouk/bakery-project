using Grpc.Core;
using MediatR;
using Reviews.Grpc;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Models;
using Reviews.Domain.ValueObjects;
using AppReviewDto = Reviews.Application.Reviews.Queries.GetReviewsList.ReviewDto;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Reviews.WebApi.GrpcServices;
public sealed class ReviewsGrpcService(ISender sender) : ReviewsGrpc.ReviewsGrpcBase
{
    private const int MaxProducts = 50;

    public override async Task<ReviewInfo> GetReview(GetReviewRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ReviewId))
            throw Invalid("review_id обов'язковий.");

        return ToInfo(await sender.Send(new GetReviewByIdQuery(request.ReviewId), context.CancellationToken));
    }

    public override async Task<ListReviewsReply> ListReviews(ListReviewsRequest request, ServerCallContext context)
    {
        var query = new GetReviewsListQuery(
            ProductId: request.HasProductId ? ParseGuid(request.ProductId, nameof(request.ProductId)) : null,
            AuthorId: request.HasAuthorId ? ParseGuid(request.AuthorId, nameof(request.AuthorId)) : null,
            Status: ParseEnum(request.Status, ReviewStatus.Approved, "status"),
            MinRating: request.HasMinRating ? request.MinRating : null,
            MaxRating: request.HasMaxRating ? request.MaxRating : null,
            Sort: ParseEnum(request.Sort, ReviewSort.Newest, "sort"),
            Page: request.Page <= 0 ? 1 : request.Page,
            PageSize: request.PageSize <= 0 ? 20 : request.PageSize);

        // Валідацію (діапазони, порожні GUID) виконує ValidationBehavior конвеєра, як і для REST.
        var page = await sender.Send(query, context.CancellationToken);

        var reply = new ListReviewsReply { Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount };
        reply.Items.AddRange(page.Items.Select(ToInfo));
        return reply;
    }

    public override async Task<GetReviewsForProductsReply> GetReviewsForProducts(
        GetReviewsForProductsRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count == 0)
            throw Invalid("product_ids не може бути порожнім.");
        if (request.ProductIds.Count > MaxProducts)
            throw Invalid($"Забагато товарів (максимум {MaxProducts}).");
        if (request.PerProductLimit is < 0 or > 100)
            throw Invalid("per_product_limit має бути від 1 до 100.");

        var limit = request.PerProductLimit == 0 ? 20 : request.PerProductLimit;
        var ids = request.ProductIds.Select(i => ParseGuid(i, "product_ids")).Distinct().ToList();

        var reply = new GetReviewsForProductsReply();
        foreach (var id in ids)
        {
            var page = await sender.Send(new GetReviewsListQuery(ProductId: id, PageSize: limit), context.CancellationToken);

            var item = new ProductReviewsInfo { ProductId = id.ToString(), TotalCount = page.TotalCount };
            item.Reviews.AddRange(page.Items.Select(ToInfo));
            reply.Products.Add(item);
        }

        return reply;
    }

    // ====================== перетворення ======================

    private static ReviewInfo ToInfo(AppReviewDto r)
    {
        var info = new ReviewInfo
        {
            ReviewId = r.Id,
            ProductId = r.ProductId.ToString(),
            ProductName = r.ProductName,
            AuthorName = r.AuthorName,
            Rating = r.Rating,
            Status = r.Status,
            PhotosCount = r.PhotosCount,
            CreatedAt = Utc(r.CreatedAt),
            Version = r.Version
        };
        if (r.Text is not null) info.Text = r.Text;
        if (r.Reply is { } reply)
            info.Reply = new ReviewReplyInfo { Text = reply.Text, AuthorName = reply.AuthorName, CreatedAt = Utc(reply.CreatedAt) };
        return info;
    }

    private static Timestamp Utc(DateTime value) => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback, string field) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw Invalid($"{field}: '{value}' невідомий. Допустимі: {string.Join(", ", Enum.GetNames<TEnum>())}.");

    private static Guid ParseGuid(string value, string field) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw Invalid($"{field}: '{value}' не є коректним GUID.");

    private static RpcException Invalid(string message) => new(new Status(StatusCode.InvalidArgument, message));
}
