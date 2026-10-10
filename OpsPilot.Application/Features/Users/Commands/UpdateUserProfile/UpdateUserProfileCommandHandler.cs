using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Users;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserProfileCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
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
