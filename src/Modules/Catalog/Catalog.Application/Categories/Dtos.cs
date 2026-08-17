namespace Catalog.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, string Slug, Guid? ParentId, bool IsActive);
