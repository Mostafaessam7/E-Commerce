using Customers.Application.Abstractions;
using Messaging;
using SharedKernel.Results;

namespace Customers.Application.Profile;

public sealed record UpdateProfileCommand(Guid CustomerId, string? FullName, string? Phone) : ICommand<Unit>;

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Unit>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomersUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(ICustomerRepository repository, ICustomersUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Customer.NotFound", "Customer profile was not found."));
        }

        customer.UpdateProfile(request.FullName, request.Phone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record AddAddressCommand(
    Guid CustomerId, string Label, string FullName, string Phone, string Line1, string? Line2,
    string City, string? State, string PostalCode, string Country, bool IsDefault) : ICommand<Guid>;

public sealed class AddAddressCommandHandler : IRequestHandler<AddAddressCommand, Guid>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomersUnitOfWork _unitOfWork;

    public AddAddressCommandHandler(ICustomerRepository repository, ICustomersUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(AddAddressCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Customer.NotFound", "Customer profile was not found."));
        }

        var result = customer.AddAddress(
            request.Label, request.FullName, request.Phone, request.Line1, request.Line2,
            request.City, request.State, request.PostalCode, request.Country, request.IsDefault);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(result.Value);
    }
}

public sealed record RemoveAddressCommand(Guid CustomerId, Guid AddressId) : ICommand<Unit>;

public sealed class RemoveAddressCommandHandler : IRequestHandler<RemoveAddressCommand, Unit>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomersUnitOfWork _unitOfWork;

    public RemoveAddressCommandHandler(ICustomerRepository repository, ICustomersUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(RemoveAddressCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Customer.NotFound", "Customer profile was not found."));
        }

        var result = customer.RemoveAddress(request.AddressId);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record SetDefaultAddressCommand(Guid CustomerId, Guid AddressId) : ICommand<Unit>;

public sealed class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, Unit>
{
    private readonly ICustomerRepository _repository;
    private readonly ICustomersUnitOfWork _unitOfWork;

    public SetDefaultAddressCommandHandler(ICustomerRepository repository, ICustomersUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Customer.NotFound", "Customer profile was not found."));
        }

        var result = customer.SetDefaultAddress(request.AddressId);
        if (result.IsFailure)
        {
            return Result.Failure<Unit>(result.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record GetCustomerProfileQuery(Guid CustomerId) : IQuery<CustomerProfileDto>;

public sealed class GetCustomerProfileQueryHandler : IRequestHandler<GetCustomerProfileQuery, CustomerProfileDto>
{
    private readonly ICustomerRepository _repository;

    public GetCustomerProfileQueryHandler(ICustomerRepository repository) => _repository = repository;

    public async Task<Result<CustomerProfileDto>> Handle(GetCustomerProfileQuery request, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerProfileDto>(Error.NotFound("Customer.NotFound", "Customer profile was not found."));
        }

        return Result.Success(GetOrCreateCustomerCommandHandler.ToDto(customer));
    }
}
