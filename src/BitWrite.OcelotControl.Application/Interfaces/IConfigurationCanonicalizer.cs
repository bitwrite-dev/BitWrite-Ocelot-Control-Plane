using BitWrite.OcelotControl.Domain.Services;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IConfigurationCanonicalizer
{
    string Canonicalize(OcelotConfiguration configuration);
    string CanonicalizeJson(OcelotConfiguration configuration);
}