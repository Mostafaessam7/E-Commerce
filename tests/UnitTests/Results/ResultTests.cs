using FluentAssertions;
using SharedKernel.Results;

namespace UnitTests.Results;

public class ResultTests
{
    [Fact]
    public void Success_result_carries_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_result_carries_the_given_error()
    {
        var error = Error.NotFound("Product.NotFound", "Product was not found.");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_success_result_exposes_its_value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Accessing_value_of_a_failed_result_throws()
    {
        var result = Result.Failure<int>(Error.Validation("Code", "message"));

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Implicit_conversion_from_value_creates_a_success_result()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Create_returns_success_for_a_non_null_value()
    {
        var result = Result.Create("value", Error.NotFound("Code", "message"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value");
    }

    [Fact]
    public void Create_returns_failure_for_a_null_value()
    {
        var error = Error.NotFound("Code", "message");

        var result = Result.Create<string>(null, error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }
}
