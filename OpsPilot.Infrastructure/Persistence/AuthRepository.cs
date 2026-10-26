using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Settings;
using OpsPilot.Application.Features.Auth.Commands.Login;
using OpsPilot.Application.Features.Auth.Commands.RegisterTenant;
using OpsPilot.Contracts.Auth;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;
using OpsPilot.Domain.Identity;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class AuthRepository : IAuthRepository
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantProvisioner _tenantProvisioner;
    private readonly ITenantRepository _tenants;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IOptionsMonitor<JwtOptions> _jwtOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(
        IUserRepository userRepository,
        ITenantProvisioner tenantProvisioner,
        ITenantRepository tenants,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptionsMonitor<JwtOptions> jwtOptions,
        TimeProvider timeProvider,
        ILogger<AuthRepository> logger)
    {
        _userRepository = userRepository;
        _tenantProvisioner = tenantProvisioner;
        _tenants = tenants;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginCommand request, CancellationToken cancellationToken = default)
    {
        // Not tenant-filtered on purpose: the email is globally unique and no tenant can be
        // resolved before the credential has been verified (ERD §3).
        var user = await _userRepository.FindByEmailAcrossTenantsAsync(request.Identifier, cancellationToken);

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

        // A closed company accepts no new sign-ins. Its Owner is the exception: they are the
        // one who can bring it back, and locking them out would make the state permanent.
        // Checked after the password so a wrong password still says "wrong password" and
        // never reveals whether the company is closed.
        var tenant = await _tenants.GetByIdAsync(user.TenantId, cancellationToken);

        if (tenant is { IsActive: false }
            && !user.Roles.Any(r =>
                string.Equals(r.Name, SystemRoleNames.Owner, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Login blocked for user {UserId}: the company is closed", user.Id);
            throw new ForbiddenException(ExceptionMessages.CompanyDormant);
        }

        user.RecordLogin(_timeProvider.GetUtcNow());
        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }    public async Task<AuthResponseDto> RegisterTenantAsync(
        RegisterTenantCommand request,
        CancellationToken cancellationToken = default)
    {
        // Sign-in is by email alone, so the address must be unique across the whole system.
        if (await _userRepository.EmailExistsAcrossTenantsAsync(request.Email, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.EmailAlreadyExists);
        }

        var provisioned = await _tenantProvisioner.ProvisionAsync(
            request.CompanyName,
            request.Slug,
            cancellationToken);

        var user = User.Create(provisioned.Tenant.Id, request.Email, request.DisplayName);
        user.SetPasswordHash(_passwordHasher.Hash(request.Password));
        user.AssignRole(provisioned.OwnerRole);
        user.RecordLogin(_timeProvider.GetUtcNow());

        await _userRepository.AddAsync(user, cancellationToken);

        _logger.LogInformation(
            "Provisioned tenant {TenantId} for owner {UserId}",
            provisioned.Tenant.Id,
            user.Id);

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
