using FluentAssertions;
using SharedKernel.Primitives;

namespace UnitTests.Primitives;

public class EntityTests
{
    private sealed class TestOrder : Entity<Guid>
    {
        public TestOrder(Guid id)
            : base(id)
        {
        }

        public void RaiseTestEvent(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    private sealed class TestProduct : Entity<Guid>
    {
        public TestProduct(Guid id)
            : base(id)
        {
        }
    }

    private sealed record TestDomainEvent : DomainEvent;

    [Fact]
    public void Entities_with_same_type_and_id_are_equal()
    {
        var id = Guid.NewGuid();
        var first = new TestOrder(id);
        var second = new TestOrder(id);

        first.Should().Be(second);
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        var first = new TestOrder(Guid.NewGuid());
        var second = new TestOrder(Guid.NewGuid());

        first.Should().NotBe(second);
    }

    [Fact]
    public void Entities_of_different_types_with_the_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();
        var order = new TestOrder(id);
        var product = new TestProduct(id);

        order.Equals(product).Should().BeFalse();
    }

    [Fact]
    public void Raised_domain_events_are_tracked_until_cleared()
    {
        var order = new TestOrder(Guid.NewGuid());
        order.RaiseTestEvent(new TestDomainEvent());

        order.DomainEvents.Should().HaveCount(1);

        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }
}
