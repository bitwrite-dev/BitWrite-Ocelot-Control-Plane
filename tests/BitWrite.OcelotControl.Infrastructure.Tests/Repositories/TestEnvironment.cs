using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// The environment a repository under test builds its keys with.
/// </summary>
/// <remarks>
/// Repositories resolve the environment from an <see cref="IEnvironmentContext"/>
/// rather than taking it per call, so every construction in these tests needs one.
/// Tests that care about isolation ask for a named environment here; the rest use
/// <see cref="Default"/> and only need a value.
/// </remarks>
internal static class TestEnvironment
{
    public const string Default = "development";

    public static EnvironmentName Of(string environment) => EnvironmentName.From(environment);

    public static IEnvironmentContext Context(string environment = Default) =>
        new FixedEnvironmentContext(EnvironmentName.From(environment));
}