using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepository.EmailExistsAsync(user.TenantId, request.Email, cancellationToken))
            {
                throw new ConflictException(ExceptionMessages.EmailAlreadyExists);
            }

            user.ChangeEmail(request.Email);
        }

        user.UpdateProfile(request.DisplayName);

        await _userRepository.UpdateAsync(user, cancellationToken);
        return user.ToDto();
    }
}
