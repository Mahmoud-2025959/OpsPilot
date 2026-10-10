using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Users.Commands.DeleteUser;

public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;

    public DeleteUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.Id);

        await _userRepository.DeleteAsync(user, cancellationToken);
    }
}
