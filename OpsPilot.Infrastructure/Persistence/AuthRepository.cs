using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Settings;
using OpsPilot.Application.Features.Auth.Commands.Login;
using OpsPilot.Application.Features.Auth.Commands.Register;
using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<JwtOptions> _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(
        AppDbContext context,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        Microsoft.Extensions.Options.IOptionsMonitor<JwtOptions> jwtOptions,
        TimeProvider timeProvider,
        ILogger<AuthRepository> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginCommand request, CancellationToken cancellationToken = default)
    {
        var normalized = request.Identifier.Trim().ToUpperInvariant();

        var user = await _context.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Login failed for {Identifier}: user not found", request.Identifier);
            throw new UnauthorizedException(ExceptionMessages.InvalidEmailOrPassword);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login blocked for user {UserId}: account is inactive", user.Id);
            throw new ForbiddenException(ExceptionMessages.AccountInactive);
        }

        if (user.PasswordHash is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for user {UserId}: invalid password", user.Id);
            throw new UnauthorizedException(ExceptionMessages.InvalidEmailOrPassword);
        }

        user.RecordLogin(_timeProvider.GetUtcNow());
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterCommand request, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.EmailExistsAsync(request.TenantId, request.Email, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.EmailAlreadyExists);
        }

        var user = User.Create(request.TenantId, request.Email, request.DisplayName);
        user.SetPasswordHash(_passwordHasher.Hash(request.Password));

        if (request.Roles is { Count: > 0 })
        {
            var distinctRoles = request.Roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var roles = await _userRepository.GetRolesByNamesAsync(user.TenantId, distinctRoles, cancellationToken);
            var foundNames = roles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = distinctRoles.Where(r => !foundNames.Contains(r)).ToList();

            if (missing.Count != 0)
            {
                throw new DomainException(ExceptionMessages.RolesDoNotExist(missing));
            }

            foreach (var role in roles)
            {
                user.AssignRole(role);
            }
        }

        await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation("Registered new user {UserId} in tenant {TenantId}", user.Id, user.TenantId);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var options = _jwtOptions.CurrentValue;

        var accessToken = await _tokenService.GenerateAccessTokenAsync(user, cancellationToken);
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, cancellationToken);

        var roles = user.Roles.Select(r => r.Name ?? string.Empty).OrderBy(n => n).ToList();
        var permissions = user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        return new AuthResponseDto(
            user.Id,
            user.TenantId,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles,
            permissions,
            accessToken,
            refreshToken.RawToken,
            _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(options.DurationInMinutes),
            refreshToken.Entity.ExpiresAt);
    }
}
