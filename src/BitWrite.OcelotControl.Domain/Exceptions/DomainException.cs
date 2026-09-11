namespace BitWrite.OcelotControl.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message, "VALIDATION_ERROR")
    {
        Errors = errors;
    }
}

public class NotFoundException : DomainException
{
    public string EntityType { get; }
    public string EntityId { get; }

    public NotFoundException(string entityType, string entityId)
        : base($"{entityType} with id '{entityId}' was not found.", "NOT_FOUND")
    {
        EntityType = entityType;
        EntityId = entityId;
    }
}

public class ConflictException : DomainException
{
    public string ConflictingEntityType { get; }
    public string ConflictingEntityId { get; }

    public ConflictException(string entityType, string entityId, string message)
        : base(message, "CONFLICT")
    {
        ConflictingEntityType = entityType;
        ConflictingEntityId = entityId;
    }
}

public class InvalidStateException : DomainException
{
    public string EntityType { get; }
    public string EntityId { get; }
    public string CurrentState { get; }
    public string ExpectedState { get; }

    public InvalidStateException(string entityType, string entityId, string currentState, string expectedState)
        : base($"{entityType} '{entityId}' is in invalid state '{currentState}'. Expected: '{expectedState}'.", "INVALID_STATE")
    {
        EntityType = entityType;
        EntityId = entityId;
        CurrentState = currentState;
        ExpectedState = expectedState;
    }
}