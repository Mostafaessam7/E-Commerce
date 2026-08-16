using Catalog.Domain;
using Catalog.Domain.Events;
using FluentAssertions;

namespace UnitTests.Catalog;

public class ProductTests
{
    private static Product CreateProduct() =>
        Product.Create("Nike T-Shirt", "Nike T-Shirt", "Comfortable cotton tee", null, brandId: null).Value;

    [Fact]
    public void Create_raises_ProductCreatedDomainEvent()
    {
        var product = CreateProduct();

        product.DomainEvents.Should().ContainSingle(e => e is ProductCreatedDomainEvent);
    }

    [Fact]
    public void Create_normalizes_the_slug()
    {
        var product = CreateProduct();

        product.Slug.Value.Should().Be("nike-t-shirt");
    }

    [Fact]
    public void AddVariant_succeeds_for_a_valid_sku_and_price()
    {
        var product = CreateProduct();

        var result = product.AddVariant("NIKE-BLK-M", 500m, "EGP", salePrice: null, barcode: null, weightKg: 0.2m);

        result.IsSuccess.Should().BeTrue();
        product.Variants.Should().ContainSingle(v => v.Id == result.Value);
    }

    [Fact]
    public void AddVariant_rejects_a_duplicate_sku()
    {
        var product = CreateProduct();
        product.AddVariant("NIKE-BLK-M", 500m, "EGP", null, null, null);

        var result = product.AddVariant("nike-blk-m", 550m, "EGP", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.DuplicateSku");
    }

    [Fact]
    public void AddVariant_rejects_a_sale_price_above_the_regular_price()
    {
        var product = CreateProduct();

        var result = product.AddVariant("NIKE-BLK-M", 500m, "EGP", salePrice: 600m, barcode: null, weightKg: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.InvalidSalePrice");
    }

    [Fact]
    public void Publish_fails_when_the_product_has_no_variants()
    {
        var product = CreateProduct();

        var result = product.Publish();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Product.NoVariants");
        product.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public void Publish_succeeds_once_a_variant_exists_and_raises_ProductPublishedDomainEvent()
    {
        var product = CreateProduct();
        product.AddVariant("NIKE-BLK-M", 500m, "EGP", null, null, null);

        var result = product.Publish();

        result.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Active);
        product.DomainEvents.Should().ContainSingle(e => e is ProductPublishedDomainEvent);
    }

    [Fact]
    public void AddImage_marking_a_new_image_primary_demotes_the_previous_primary_image()
    {
        var product = CreateProduct();
        product.AddImage("https://cdn/1.jpg", isPrimary: true);

        product.AddImage("https://cdn/2.jpg", isPrimary: true);

        product.Images.Single(i => i.Url == "https://cdn/1.jpg").IsPrimary.Should().BeFalse();
        product.Images.Single(i => i.Url == "https://cdn/2.jpg").IsPrimary.Should().BeTrue();
    }
}
