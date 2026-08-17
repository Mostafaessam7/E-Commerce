using Messaging;
using Shipping.Application.Abstractions;
using Shipping.Domain;
using SharedKernel.Results;

namespace Shipping.Application.Methods;

public sealed record CreateShippingMethodCommand(
    string Name, string? Description, decimal Cost, string Currency, int? EstimatedDaysMin, int? EstimatedDaysMax) : ICommand<Guid>;

public sealed class CreateShippingMethodCommandHandler : IRequestHandler<CreateShippingMethodCommand, Guid>
{
    private readonly IShippingMethodRepository _repository;
    private readonly IShippingUnitOfWork _unitOfWork;

    public CreateShippingMethodCommandHandler(IShippingMethodRepository repository, IShippingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateShippingMethodCommand request, CancellationToken cancellationToken = default)
    {
        var result = ShippingMethod.Create(
            request.Name, request.Description, request.Cost, request.Currency, request.EstimatedDaysMin, request.EstimatedDaysMax);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var method = result.Value;
        await _repository.AddAsync(method, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(method.Id);
    }
}

public sealed record DeactivateShippingMethodCommand(Guid ShippingMethodId) : ICommand<Unit>;

public sealed class DeactivateShippingMethodCommandHandler : IRequestHandler<DeactivateShippingMethodCommand, Unit>
{
    private readonly IShippingMethodRepository _repository;
    private readonly IShippingUnitOfWork _unitOfWork;

    public DeactivateShippingMethodCommandHandler(IShippingMethodRepository repository, IShippingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(DeactivateShippingMethodCommand request, CancellationToken cancellationToken = default)
    {
        var method = await _repository.GetByIdAsync(request.ShippingMethodId, cancellationToken);
        if (method is null)
        {
            return Result.Failure<Unit>(Error.NotFound("ShippingMethod.NotFound", "Shipping method was not found."));
        }

        method.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}

public sealed record ActivateShippingMethodCommand(Guid ShippingMethodId) : ICommand<Unit>;

public sealed class ActivateShippingMethodCommandHandler : IRequestHandler<ActivateShippingMethodCommand, Unit>
{
    private readonly IShippingMethodRepository _repository;
    private readonly IShippingUnitOfWork _unitOfWork;

    public ActivateShippingMethodCommandHandler(IShippingMethodRepository repository, IShippingUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(ActivateShippingMethodCommand request, CancellationToken cancellationToken = default)
    {
        var method = await _repository.GetByIdAsync(request.ShippingMethodId, cancellationToken);
        if (method is null)
        {
            return Result.Failure<Unit>(Error.NotFound("ShippingMethod.NotFound", "Shipping method was not found."));
        }

        method.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(Unit.Value);
    }
}
