using FluentValidation;
using OpsPilot.Application.Common.Constants;

namespace OpsPilot.Application.Features.SlaPolicies.Commands.UpdateSlaPolicies;

public sealed class UpdateSlaPoliciesCommandValidator : AbstractValidator<UpdateSlaPoliciesCommand>
{
    public UpdateSlaPoliciesCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationMessages.TenantIdRequired);

        RuleFor(x => x.Policies)
            .NotEmpty().WithMessage(ValidationMessages.SlaPoliciesRequired);

        // The database keys policies by severity, so two entries for the same severity would race
        // to write the same row. Refusing the duplicate is clearer than an arbitrary winner.
        RuleFor(x => x.Policies)
            .Must(policies => policies.Select(p => p.Severity).Distinct().Count() == policies.Count)
            .WithMessage(ValidationMessages.SlaSeveritiesMustBeUnique)
            .When(x => x.Policies is { Count: > 0 });

        RuleForEach(x => x.Policies).ChildRules(policy =>
        {
            policy.RuleFor(p => p.Severity)
                .IsInEnum().WithMessage(ValidationMessages.SlaSeverityInvalid);

            policy.RuleFor(p => p.ResponseMinutes)
                .GreaterThan(0).WithMessage(ValidationMessages.SlaResponseMustBePositive);

            policy.RuleFor(p => p.ResolutionMinutes)
                .GreaterThan(0).WithMessage(ValidationMessages.SlaResolutionMustBePositive);

            // Resolution must trail response, or "resolved on time" would be looser than
            // "responded on time". The domain enforces this too; catching it here gives a 400
            // with a readable message instead of a 400 out of the domain layer.
            policy.RuleFor(p => p.ResolutionMinutes)
                .GreaterThan(p => p.ResponseMinutes)
                .WithMessage(ValidationMessages.SlaResolutionMustExceedResponse);
        });
    }
}
