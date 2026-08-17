using Customers.Application.Abstractions;
using Customers.Domain;
using Messaging;
using SharedKernel.Results;

namespace Customers.Application.Profile;

/// <summary>Same "create if missing, otherwise return what exists" shape as Ordering's
/// <c>GetOrCreateCartCommand</c> — called once per authenticated request that needs a profile,
/// idempotent by construction (never fails just because the profile already exists).</summary>
public sealed record GetOrCreateCustomerCommand(Guid CustomerId, string Email) : ICommand<CustomerProfileDto>;

public sealed class GetOrCreateCustomerCommandHandler : IRequestHandler<GetOrCreateCustomerCommand, CustomerProfileDto>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomersUnitOfWork _unitOfWork;

    public GetOrCreateCustomerCommandHandler(ICustomerRepository repository, ICustomersUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CustomerProfileDto>> Handle(GetOrCreateCustomerCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);

        if (customer is null)
        {
            var createResult = Customer.Create(request.CustomerId, request.Email);
            if (createResult.IsFailure)
            {
                return Result.Failure<CustomerProfileDto>(createResult.Error);
            }

            customer = createResult.Value;
            await _repository.AddAsync(customer, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(ToDto(customer));
    }

    internal static CustomerProfileDto ToDto(Customer customer) => new(
        customer.Id,
        customer.Email,
        customer.FullName,
        customer.Phone,
        customer.Addresses.Select(a => new CustomerAddressDto(
            a.Id, a.Label, a.FullName, a.Phone, a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country, a.IsDefault)).ToList());
}
