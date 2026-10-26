using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Behaviors;
using OpsPilot.Application.Features.ApiKeys;
using OpsPilot.Application.Features.Integration.Handlers;
using OpsPilot.Application.Features.Roles;
using OpsPilot.Application.Features.Webhooks;

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

        // The same story for a webhook's signing secret: minting it, hashing it and storing
        // the encrypted copy is one routine, and both registering an endpoint and rotating its
        // secret are that routine.
        services.AddScoped<WebhookIssuer>();

        // Creating a role and changing what one grants are both bound by the same rule --
        // nobody grants a permission they do not hold -- so the rule lives in one place.
        services.AddScoped<RoleGrantGuard>();

        // The handlers the broker consumer dispatches to. Registering a handler is the whole
        // of what it takes to answer a new kind of integration event.
        services.AddScoped<IIntegrationEventHandler, IngestedEventHandler>();

        return services;
    }
}