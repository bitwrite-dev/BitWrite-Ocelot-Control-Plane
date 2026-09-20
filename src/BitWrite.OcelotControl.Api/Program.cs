using BitWrite.OcelotControl.Api.Middleware;
using BitWrite.OcelotControl.Api.Validators;
using AppInterfaces = BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Application.UseCases.Publication;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Application.UseCases.Service;
using BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using BitWrite.OcelotControl.Application.UseCases.Plugin;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using BitWrite.OcelotControl.Application.UseCases.License;
using BitWrite.OcelotControl.Application.Events;
using DomainServices = BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Infrastructure.Adapters;
using InfraAdapters = BitWrite.OcelotControl.Infrastructure.Adapters;
using BitWrite.OcelotControl.Infrastructure.Outbox;
using BitWrite.OcelotControl.Infrastructure.Redis;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using RuntimeAdapters = BitWrite.OcelotControl.Runtime.Adapters;
using FluentValidation.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();

            // Register validators
            builder.Services.AddValidatorsFromAssemblyContaining<Program>();

            // Redis Connection
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

            // Domain Services (registered as concrete for internal use)
            builder.Services.AddScoped<DomainServices.ConfigurationBuilder>();
            builder.Services.AddScoped<DomainServices.ConfigurationCanonicalizer>();
            builder.Services.AddScoped<DomainServices.RouteConflictDetector>();
            builder.Services.AddScoped<DomainServices.ConfigurationConsistencyValidator>();
            builder.Services.AddScoped<DomainServices.OcelotCapabilityResolver>();
            builder.Services.AddScoped<DomainServices.SnapshotIntegrityVerifier>();
            builder.Services.AddScoped<DomainServices.SnapshotVersionAllocator>();

            // Infrastructure Adapters (implement Application.Interfaces for DI)
builder.Services.AddScoped<AppInterfaces.IConfigurationBuilder, ConfigurationBuilderAdapter>();
            builder.Services.AddScoped<AppInterfaces.IConfigurationCanonicalizer, ConfigurationCanonicalizerAdapter>();
            builder.Services.AddScoped<AppInterfaces.IRouteConflictDetector, RouteConflictDetectorAdapter>();
            builder.Services.AddScoped<AppInterfaces.IConfigurationConsistencyValidator, ConfigurationConsistencyValidatorAdapter>();
            builder.Services.AddScoped<AppInterfaces.IOcelotCapabilityResolver, OcelotCapabilityResolverAdapter>();
            builder.Services.AddScoped<AppInterfaces.ISnapshotIntegrityVerifier, SnapshotIntegrityVerifierAdapter>();
            builder.Services.AddScoped<AppInterfaces.ISnapshotVersionAllocator, SnapshotVersionAllocatorAdapter>();

            // Repository Implementations
            builder.Services.AddScoped<AppInterfaces.IGatewayRepository, RedisGatewayRepository>();
            builder.Services.AddScoped<AppInterfaces.IRouteRepository, RedisRouteRepository>();
            builder.Services.AddScoped<AppInterfaces.IServiceRepository, RedisServiceRepository>();
            builder.Services.AddScoped<AppInterfaces.ISnapshotRepository, RedisSnapshotRepository>();
            builder.Services.AddScoped<AppInterfaces.IPublicationRepository, RedisPublicationRepository>();
            builder.Services.AddScoped<AppInterfaces.IGlobalConfigurationRepository, RedisGlobalConfigurationRepository>();
            builder.Services.AddScoped<AppInterfaces.IRuntimeInstanceRepository, RuntimeInstanceRepositoryAdapter>();
            builder.Services.AddScoped<AppInterfaces.IPluginRepository, PluginRepositoryAdapter>();
            builder.Services.AddScoped<AppInterfaces.ILicenseRepository, RedisLicenseRepository>();

            // Infrastructure Services
            builder.Services.AddSingleton<AppInterfaces.IDistributedLock, RedisDistributedLock>();
            builder.Services.AddSingleton<AppInterfaces.IRedisPublisher, RedisPublisher>();
            builder.Services.AddSingleton<IOutboxRepository, RedisOutboxRepository>();
            builder.Services.AddScoped<AppInterfaces.IOutboxRepository, OutboxRepositoryAdapter>();

            // IOcelotConfigApplier Implementation (Infrastructure.Adapters - real impl with Redis/File providers)
            builder.Services.AddSingleton<InfraAdapters.IConfigurationProvider, InfraAdapters.RedisConfigurationProvider>();
            builder.Services.AddSingleton<AppInterfaces.IOcelotConfigApplier, InfraAdapters.OcelotConfigApplier>();

            builder.Services.AddSingleton<JsonEventSerializer>();
            builder.Services.AddScoped<AppInterfaces.IEventSerializer, EventSerializerAdapter>();

            // UseCase Handlers (Commands) - EXISTING
            builder.Services.AddScoped<CreateSnapshotCommandHandler>();
            builder.Services.AddScoped<PublishSnapshotCommandHandler>();
            builder.Services.AddScoped<RollbackSnapshotCommandHandler>();

            // Route UseCase Handlers
            builder.Services.AddScoped<CreateRouteCommandHandler>();
            builder.Services.AddScoped<UpdateRouteCommandHandler>();
            builder.Services.AddScoped<DeleteRouteCommandHandler>();
            builder.Services.AddScoped<EnableRouteCommandHandler>();
            builder.Services.AddScoped<DisableRouteCommandHandler>();
            builder.Services.AddScoped<GetRouteStatusCommandHandler>();
            builder.Services.AddScoped<ValidateRouteCommandHandler>();
            builder.Services.AddScoped<PreviewRouteQueryHandler>();
            builder.Services.AddScoped<GetEffectiveRouteQueryHandler>();
            builder.Services.AddScoped<ListRoutesQueryHandler>();
            builder.Services.AddScoped<GetRouteQueryHandler>();
            builder.Services.AddScoped<RouteHistoryQueryHandler>();

            // Service UseCase Handlers
            builder.Services.AddScoped<CreateServiceCommandHandler>();
            builder.Services.AddScoped<UpdateServiceCommandHandler>();
            builder.Services.AddScoped<DeleteServiceCommandHandler>();
            builder.Services.AddScoped<GetServiceQueryHandler>();
            builder.Services.AddScoped<ListServicesQueryHandler>();
            builder.Services.AddScoped<GetServiceRoutesQueryHandler>();
            // GlobalConfiguration UseCase Handlers
            builder.Services.AddScoped<GetGlobalConfigurationQueryHandler>();
            builder.Services.AddScoped<UpdateGlobalConfigurationCommandHandler>();
            // Snapshot UseCase Handlers
            builder.Services.AddScoped<ListSnapshotsQueryHandler>();
            builder.Services.AddScoped<GetSnapshotQueryHandler>();
            builder.Services.AddScoped<ValidateSnapshotCommandHandler>();
            builder.Services.AddScoped<CompareSnapshotsQueryHandler>();
            builder.Services.AddScoped<CloneSnapshotCommandHandler>();
            builder.Services.AddScoped<ExportSnapshotQueryHandler>();
            builder.Services.AddScoped<GetSnapshotDeploymentQueryHandler>();
            // Publication UseCase Handlers
            builder.Services.AddScoped<ListPublicationsQueryHandler>();
            builder.Services.AddScoped<GetCurrentPublicationQueryHandler>();
            builder.Services.AddScoped<GetPublicationHistoryQueryHandler>();
            // Plugin UseCase Handlers
            builder.Services.AddScoped<InstallPluginCommandHandler>();
            builder.Services.AddScoped<GetPluginQueryHandler>();
            builder.Services.AddScoped<UninstallPluginCommandHandler>();
            builder.Services.AddScoped<EnablePluginCommandHandler>();
            builder.Services.AddScoped<DisablePluginCommandHandler>();
            builder.Services.AddScoped<ListPluginsQueryHandler>();
            builder.Services.AddScoped<UpgradePluginCommandHandler>();
            // Runtime UseCase Handlers
            builder.Services.AddScoped<GetRuntimeStatusQueryHandler>();
            builder.Services.AddScoped<GetAllGatewaysQueryHandler>();
            builder.Services.AddScoped<GetGatewayRuntimeDetailQueryHandler>();
            builder.Services.AddScoped<ReconcileGatewayCommandHandler>();
            // License UseCase Handlers
            builder.Services.AddScoped<CreateLicenseCommandHandler>();
            builder.Services.AddScoped<GetLicenseQueryHandler>();
            builder.Services.AddScoped<ListLicensesQueryHandler>();
            builder.Services.AddScoped<ActivateLicenseCommandHandler>();
            builder.Services.AddScoped<UpdateLicenseCommandHandler>();
            builder.Services.AddScoped<RenewLicenseCommandHandler>();
            builder.Services.AddScoped<RevokeLicenseCommandHandler>();
            // Gateway UseCase Handlers
            builder.Services.AddScoped<RegisterGatewayCommandHandler>();
            builder.Services.AddScoped<GetGatewayQueryHandler>();
            builder.Services.AddScoped<ListGatewaysQueryHandler>();
            builder.Services.AddScoped<UpdateGatewayCommandHandler>();
            builder.Services.AddScoped<UpdateGatewayStatusCommandHandler>();

            // Domain Event Dispatcher
            builder.Services.AddScoped<AppInterfaces.IDomainEventDispatcher, DomainEventDispatcher>();

            // Background Services
            builder.Services.AddHostedService<OutboxPublisher>();
            builder.Services.AddHostedService<RuntimeAdapters.RuntimeAdapter>();

            // Add Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() 
                { 
                    Title = "BitWrite Ocelot Control Plane API", 
                    Version = "v1",
                    Description = "Management API for Ocelot Gateway Configuration"
                });
                
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                
                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Add JWT Authentication
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = builder.Configuration["Authentication:Authority"];
                    options.Audience = builder.Configuration["Authentication:Audience"];
                    options.RequireHttpsMetadata = false; // For development
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
                options.AddPolicy("GatewayManager", policy => policy.RequireRole("Admin", "GatewayManager"));
                options.AddPolicy("RouteManager", policy => policy.RequireRole("Admin", "RouteManager"));
                options.AddPolicy("SnapshotManager", policy => policy.RequireRole("Admin", "SnapshotManager"));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseMiddleware<GlobalExceptionMiddleware>();
            app.UseMiddleware<CorrelationIdMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}