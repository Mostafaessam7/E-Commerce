using Customers.Domain;
using FluentAssertions;

namespace UnitTests.Customers;

public class CustomerTests
{
    private static Customer CreateCustomer() => Customer.Create(Guid.NewGuid(), "buyer@example.com").Value;

    [Fact]
    public void First_address_added_becomes_the_default_automatically()
    {
        var customer = CreateCustomer();

        var result = customer.AddAddress("Home", "Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG", isDefault: false);

        result.IsSuccess.Should().BeTrue();
        customer.Addresses.Should().ContainSingle(a => a.Id == result.Value && a.IsDefault);
    }

    [Fact]
    public void Adding_a_second_address_as_default_makes_the_first_one_non_default()
    {
        var customer = CreateCustomer();
        customer.AddAddress("Home", "Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG", isDefault: false);
        var secondResult = customer.AddAddress("Work", "Ahmed Ali", "+201000000001", "2 Test St", null, "Giza", null, "12511", "EG", isDefault: true);

        customer.Addresses.Should().ContainSingle(a => a.IsDefault && a.Id == secondResult.Value);
    }

    [Fact]
    public void Removing_the_default_address_promotes_another_one_if_any_remain()
    {
        var customer = CreateCustomer();
        var firstId = customer.AddAddress("Home", "Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG", isDefault: false).Value;
        var secondId = customer.AddAddress("Work", "Ahmed Ali", "+201000000001", "2 Test St", null, "Giza", null, "12511", "EG", isDefault: false).Value;

        var removeResult = customer.RemoveAddress(firstId);

        removeResult.IsSuccess.Should().BeTrue();
        customer.Addresses.Should().ContainSingle(a => a.Id == secondId && a.IsDefault);
    }

    [Fact]
    public void Removing_an_address_that_does_not_exist_fails()
    {
        var customer = CreateCustomer();

        var result = customer.RemoveAddress(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Customer.AddressNotFound");
    }

    [Fact]
    public void SetDefaultAddress_moves_the_default_flag_to_exactly_one_address()
    {
        var customer = CreateCustomer();
        var firstId = customer.AddAddress("Home", "Ahmed Ali", "+201000000000", "1 Test St", null, "Cairo", null, "11511", "EG", isDefault: false).Value;
        var secondId = customer.AddAddress("Work", "Ahmed Ali", "+201000000001", "2 Test St", null, "Giza", null, "12511", "EG", isDefault: false).Value;

        var result = customer.SetDefaultAddress(secondId);

        result.IsSuccess.Should().BeTrue();
        customer.Addresses.Single(a => a.Id == firstId).IsDefault.Should().BeFalse();
        customer.Addresses.Single(a => a.Id == secondId).IsDefault.Should().BeTrue();
    }
}
