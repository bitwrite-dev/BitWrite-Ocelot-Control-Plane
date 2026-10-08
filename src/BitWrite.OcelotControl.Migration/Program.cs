using Microsoft.Extensions.Hosting;
﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

using BitWrite.OcelotControl.Migration;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables(prefix: "MIGRATION_");
builder.Configuration.AddCommandLine(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configuration
var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    ?? builder.Configuration["redis"]
    ?? throw new InvalidOperationException(
        "Redis connection string required. Set ConnectionStrings:redis or MIGRATION_REDIS environment variable.");

var targetEnvironment = builder.Configuration["targetEnvironment"]
    ?? builder.Configuration["MIGRATION_TARGET_ENVIRONMENT"]
    ?? "development";

var deleteOldKeys = bool.TryParse(builder.Configuration["deleteOldKeys"], out var del) && del;

var migrationType = builder.Configuration["migrationType"]
    ?? builder.Configuration["MIGRATION_TYPE"]
    ?? "route";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
    logger.LogInformation("Connected to Redis");
    return multiplexer;
});

builder.Services.AddSingleton<RouteKeyMigrator>(sp =>
    new RouteKeyMigrator(
        sp.GetRequiredService<IConnectionMultiplexer>(),
        targetEnvironment,
        deleteOldKeys,
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RouteKeyMigrator>>()));

builder.Services.AddSingleton<ServiceKeyMigrator>(sp =>
    new ServiceKeyMigrator(
        sp.GetRequiredService<IConnectionMultiplexer>(),
        targetEnvironment,
        deleteOldKeys,
        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServiceKeyMigrator>>()));

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting {Type} key migration to environment '{Environment}' (deleteOldKeys={DeleteOldKeys})",
    migrationType, targetEnvironment, deleteOldKeys);

int exitCode = 0;

if (migrationType.Equals("route", StringComparison.OrdinalIgnoreCase))
{
    var migrator = host.Services.GetRequiredService<RouteKeyMigrator>();
    var result = await migrator.MigrateAsync();
    LogResult(result);
    if (!result.Success) exitCode = 1;
}
else if (migrationType.Equals("service", StringComparison.OrdinalIgnoreCase))
{
    var migrator = host.Services.GetRequiredService<ServiceKeyMigrator>();
    var result = await migrator.MigrateAsync();
    LogResult(result);
    if (!result.Success) exitCode = 1;
}
else
{
    logger.LogError("Unknown migration type: {Type}. Use 'route' or 'service'.", migrationType);
    exitCode = 2;
}

return exitCode;

void LogResult(MigrationResult result)
{
    if (result.Success)
    {
        logger.LogInformation("Migration completed successfully: {Count} migrated, {Deleted} old keys deleted",
            result.MigratedCount, result.DeletedOldKeys);
    }
    else
    {
        logger.LogError("Migration completed with errors: {Errors}", string.Join("; ", result.Errors));
    }
}