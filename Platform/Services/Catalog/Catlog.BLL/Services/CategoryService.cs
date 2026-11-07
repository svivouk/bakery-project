using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Bll.Interfaces;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Models;

namespace Catalog.Bll.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool activeOnly, CancellationToken ct = default)
    {
        var categories = await _uow.Categories.GetAllAsync(activeOnly, ct);
        return _mapper.Map<List<CategoryDto>>(categories);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException("Category", id);
        return _mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = new Category
        {
            Name = Check.Text(request.Name, "name", 100),
            Slug = Check.Slug(request.Slug, "slug", 120),
            Description = Check.OptionalText(request.Description, "description", 500),
            SortOrder = request.SortOrder,
            IsActive = true
        };

        // Дружнє повідомлення; остаточну гарантію унікальності дає UX_Categories_Slug (дублікат → 409).
        if (await _uow.Categories.GetBySlugAsync(category.Slug, ct) is not null)
            throw new ConflictException($"Категорія зі slug '{category.Slug}' уже існує.");

        var id = await _uow.Categories.AddAsync(category, ct);
        return await GetAsync(id, ct);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = new Category
        {
            CategoryId = id,
            Name = Check.Text(request.Name, "name", 100),
            Slug = Check.Slug(request.Slug, "slug", 120),
            Description = Check.OptionalText(request.Description, "description", 500),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        var sameSlug = await _uow.Categories.GetBySlugAsync(category.Slug, ct);
        if (sameSlug is not null && sameSlug.CategoryId != id)
            throw new ConflictException($"Категорія зі slug '{category.Slug}' уже існує.");

        if (!await _uow.Categories.UpdateAsync(category, ct))
            throw new NotFoundException("Category", id);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await _uow.Categories.HasProductsAsync(id, ct))
            throw new ConflictException("Категорію не можна видалити: у ній є товари.");

        if (!await _uow.Categories.DeleteAsync(id, ct))
            throw new NotFoundException("Category", id);
    }
}
