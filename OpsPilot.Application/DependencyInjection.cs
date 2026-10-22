using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Behaviors;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Application.Features.Roles;

namespace OpsPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        // Issuing and rotating a key are the same three steps over a shared repository and
        // protector, so they live in one place rather than in each command handler.
        services.AddScoped<ApiKeyIssuer>();

        // Creating a role and changing what one grants are both bound by the same rule --
        // nobody grants a permission they do not hold -- so the rule lives in one place.
        services.AddScoped<RoleGrantGuard>();

        return services;
    }
}