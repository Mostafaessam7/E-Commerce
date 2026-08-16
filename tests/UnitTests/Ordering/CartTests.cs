using FluentAssertions;
using Ordering.Domain;

namespace UnitTests.Ordering;

public class CartTests
{
    private static readonly Guid VariantId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    [Fact]
    public void AddItem_adds_a_new_line_for_a_new_variant()
    {
        var cart = Cart.CreateForGuest(Guid.NewGuid());

        var result = cart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 2);

        result.IsSuccess.Should().BeTrue();
        cart.Items.Should().ContainSingle(i => i.ProductVariantId == VariantId && i.Quantity == 2);
        cart.Subtotal.Amount.Should().Be(1000m);
    }

    [Fact]
    public void AddItem_merges_quantity_when_the_same_variant_is_added_again()
    {
        var cart = Cart.CreateForGuest(Guid.NewGuid());
        cart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 1);

        cart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 2);

        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(3);
    }

    [Fact]
    public void RemoveItem_fails_for_an_item_that_does_not_exist()
    {
        var cart = Cart.CreateForGuest(Guid.NewGuid());

        var result = cart.RemoveItem(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ItemNotFound");
    }

    [Fact]
    public void ChangeItemQuantity_updates_the_line_quantity()
    {
        var cart = Cart.CreateForGuest(Guid.NewGuid());
        cart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 1);
        var itemId = cart.Items.Single().Id;

        var result = cart.ChangeItemQuantity(itemId, 5);

        result.IsSuccess.Should().BeTrue();
        cart.Items.Single().Quantity.Should().Be(5);
    }

    [Fact]
    public void MergeFrom_copies_guest_cart_items_and_empties_the_guest_cart()
    {
        var guestCart = Cart.CreateForGuest(Guid.NewGuid());
        guestCart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 2);

        var customerCart = Cart.CreateForCustomer(Guid.NewGuid());
        customerCart.MergeFrom(guestCart);

        customerCart.Items.Should().ContainSingle(i => i.ProductVariantId == VariantId && i.Quantity == 2);
        guestCart.Items.Should().BeEmpty();
    }

    [Fact]
    public void MergeFrom_combines_quantities_for_a_variant_already_in_the_customer_cart()
    {
        var guestCart = Cart.CreateForGuest(Guid.NewGuid());
        guestCart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 2);

        var customerCart = Cart.CreateForCustomer(Guid.NewGuid());
        customerCart.AddItem(VariantId, ProductId, "Nike T-Shirt", "NIKE-BLK-M", 500m, "EGP", 1);

        customerCart.MergeFrom(guestCart);

        customerCart.Items.Single().Quantity.Should().Be(3);
    }
}
