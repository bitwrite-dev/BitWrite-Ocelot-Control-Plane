using System.Collections.Generic;

namespace BitWrite.OcelotControl.Domain;

public abstract record ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, component) =>
            {
                unchecked
                {
                    return current * 23 + (component?.GetHashCode() ?? 0);
                }
            });
    }
}