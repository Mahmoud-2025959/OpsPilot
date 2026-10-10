using Microsoft.AspNetCore.Identity;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Identity;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _inner = new();

    public string Hash(string password)
    {
        return _inner.HashPassword(null!, password);
    }

    public bool Verify(string password, string passwordHash)
    {
        return _inner.VerifyHashedPassword(null!, passwordHash, password)
            != PasswordVerificationResult.Failed;
    }
}
