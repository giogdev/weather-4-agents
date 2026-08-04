using System.ComponentModel.DataAnnotations;
using Weather4Agents.Domain.ValueObjects;

namespace Weather4Agents.API.Validation;

/// <summary>
/// Validates a location route parameter against the domain rules in
/// <see cref="LocationName"/>; a failure becomes an automatic <c>400</c>
/// ProblemDetails via <c>[ApiController]</c> model validation.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ValidLocationAttribute : ValidationAttribute
{
    public ValidLocationAttribute()
        : base("The location must contain only letters, spaces, apostrophes or hyphens, "
               + $"with a maximum length of {LocationName.MaxLength} characters.")
    {
    }

    public override bool IsValid(object? value)
        => value is string location && LocationName.IsValid(location);
}
