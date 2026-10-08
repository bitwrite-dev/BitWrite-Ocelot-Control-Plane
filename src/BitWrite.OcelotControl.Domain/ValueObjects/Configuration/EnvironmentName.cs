using System.Collections.Generic;
using System.Linq;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

/// <summary>
/// Which environment a piece of configuration belongs to.
/// </summary>
/// <remarks>
/// Not an enum. The set of environments is a deployment decision — a team may run
/// development and production only, or add staging — so an enum would force a
/// code change and a redeploy to introduce one, and would reject the names that
/// operator already has in their infrastructure.
///
/// A value rather than a free string so that "Production", "production" and
/// "PRODUCTION" cannot become three different key prefixes pointing at three
/// different copies of the same environment.
/// </remarks>
public record EnvironmentName : ValueObject
{
    /// <summary>Separator Redis keys use between segments.</summary>
    public const char Separator = ':';

    private EnvironmentName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EnvironmentName From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Environment name cannot be empty", "INVALID_ENVIRONMENT_NAME");

        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.Any(c => c == Separator || char.IsWhiteSpace(c)))
            throw new DomainException(
                $"Environment name '{value}' cannot contain a colon or whitespace, because it is a segment of a Redis key",
                "INVALID_ENVIRONMENT_NAME");

        return new EnvironmentName(trimmed);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
