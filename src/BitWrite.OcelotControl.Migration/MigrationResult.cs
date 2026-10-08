namespace BitWrite.OcelotControl.Migration;

/// <summary>Result of a key migration operation.</summary>
internal sealed class MigrationResult
{
    public string TargetEnvironment { get; set; } = "";
    public int MigratedCount { get; set; }
    public int DeletedOldKeys { get; set; }
    public bool IndexRebuilt { get; set; }
    public List<string> Errors { get; } = new();

    public bool Success => Errors.Count == 0;
}

/// <summary>Logger interface for migration tools.</summary>
internal interface ILogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
    void LogDebug(string message, params object[] args);
}
