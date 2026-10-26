using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;

    public DeleteUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        => _userRepository.ExecuteWithOwnerGuardAsync(
            ct => DeleteAsync(request.Id, ct),
            cancellationToken);

    private async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        // A deleted account is deactivated (ERD §4: users are never hard-deleted), so the same
        // rule that protects the last active Owner on the status path has to apply here too.
        if (user.IsActive
            && user.HasRole(SystemRoleNames.Owner)
            && !await _userRepository.AnotherActiveOwnerExistsAsync(user.Id, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.LastOwnerCannotBeDeleted);
        }

        await _userRepository.DeleteAsync(user, cancellationToken);
    }
}
