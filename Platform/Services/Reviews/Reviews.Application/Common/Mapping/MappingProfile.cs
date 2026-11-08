using AutoMapper;
using MongoDB.Bson;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Entities;

namespace Reviews.Application.Common.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ObjectId → string: один глобальний конвертер для Review.Id і будь-якого іншого ObjectId.
        CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());

        // VO Author розгортається в плоске поле AuthorName; відповідь персоналу теж несе лише ім'я автора.
        CreateMap<Comment, ReplyDto>()
            .ForCtorParam(nameof(ReplyDto.AuthorName), o => o.MapFrom(c => c.Author.DisplayName));

        CreateMap<Review, ReviewDto>()
            .ForCtorParam(nameof(ReviewDto.AuthorName), o => o.MapFrom(r => r.Author.DisplayName))
            .ForCtorParam(nameof(ReviewDto.PhotosCount), o => o.MapFrom(r => r.Photos.Count));
        // Id (ObjectId → string), Status (enum → string), Reply (Comment → ReplyDto), Version, CreatedAt: за іменами.
    }
}