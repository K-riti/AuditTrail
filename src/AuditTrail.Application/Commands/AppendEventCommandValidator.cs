using FluentValidation;

namespace AuditTrail.Application.Commands;

/// <summary>
/// Validator for AppendEventCommand.
/// </summary>
public class AppendEventCommandValidator : AbstractValidator<AppendEventCommand>
{
    public AppendEventCommandValidator()
    {
        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("EntityId is required")
            .MaximumLength(500).WithMessage("EntityId must not exceed 500 characters");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("EntityType is required")
            .MaximumLength(200).WithMessage("EntityType must not exceed 200 characters");

        RuleFor(x => x.EventType)
            .NotEmpty().WithMessage("EventType is required")
            .MaximumLength(200).WithMessage("EventType must not exceed 200 characters");

        RuleFor(x => x.OccurredBy)
            .NotEmpty().WithMessage("OccurredBy is required")
            .MaximumLength(500).WithMessage("OccurredBy must not exceed 500 characters");

        RuleFor(x => x.ExpectedVersion)
            .GreaterThanOrEqualTo(0).WithMessage("ExpectedVersion must be non-negative");

        RuleFor(x => x.Payload)
            .NotNull().WithMessage("Payload is required");

        RuleFor(x => x.SchemaVersion)
            .GreaterThan(0).WithMessage("SchemaVersion must be positive");
    }
}
