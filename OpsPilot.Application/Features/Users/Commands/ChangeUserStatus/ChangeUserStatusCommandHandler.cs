using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.ChangeUserStatus;

public sealed class ChangeUserStatusCommandHandler : IRequestHandler<ChangeUserStatusCommand, UserDto>
{
    private readonly IUserRepository _userRepository;

    public ChangeUserStatusCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<UserDto> Handle(ChangeUserStatusCommand request, CancellationToken cancellationToken)
        => _userRepository.ExecuteWithOwnerGuardAsync(
            ct => ChangeStatusAsync(request, ct),
            cancellationToken);

    private async Task<UserDto> ChangeStatusAsync(ChangeUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (user.TenantId != request.TenantId)
        {
            throw new EntityNotFoundException("User", request.UserId);
        }

        // Sign-in already rejects inactive accounts, but a company whose last Owner is switched
        // off has nobody who can switch it back on.
        if (!request.IsActive
            && user.IsActive
            && user.HasRole(SystemRoleNames.Owner)
            && !await _userRepository.AnotherActiveOwnerExistsAsync(user.Id, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.LastOwnerCannotBeDeactivated);
        }

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        return user.ToDto();
    }
}
