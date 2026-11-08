using AutoMapper;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.WebApi.Contracts;

namespace Reviews.WebApi.Mapping;
public sealed class ApiMappingProfile : Profile
{
    public ApiMappingProfile() => CreateMap<CreateReviewRequest, CreateReviewCommand>();
}
