namespace BitWrite.OcelotControl.Domain.ValueObjects.Identity;

/// <summary>
/// Strongly-typed ID for AuditLog entities.
/// </summary>
public readonly record struct AuditLogId
{
    public string Value { get; }

    public AuditLogId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new Exceptions.DomainException("AuditLog ID cannot be empty", "INVALID_AUDIT_LOG_ID");

        Value = value.Trim();
    }

    public static AuditLogId New() => new($"audit_{Guid.NewGuid():N}");

    public static implicit operator string(AuditLogId id) => id.Value;
    public static implicit operator AuditLogId(string value) => new(value);

    public override string ToString() => Value;
}
