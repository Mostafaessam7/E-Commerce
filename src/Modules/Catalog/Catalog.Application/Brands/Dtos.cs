namespace Catalog.Application.Brands;

public sealed record BrandDto(Guid Id, string Name, string Slug, bool IsActive);
