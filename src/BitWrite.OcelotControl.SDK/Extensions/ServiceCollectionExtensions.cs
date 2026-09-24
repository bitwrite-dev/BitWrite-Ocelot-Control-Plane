using System;
using System.Net.Http;
using BitWrite.OcelotControl.SDK.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BitWrite.OcelotControl.SDK.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOcelotControlSdk(this IServiceCollection services, Action<OcelotControlClientOptions> configureOptions)
    {
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        services.Configure(configureOptions);
        services.AddHttpClient<OcelotControlClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OcelotControlClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();
        services.AddTransient<OcelotControlClient>();

        return services;
    }

    public static IServiceCollection AddOcelotControlSdk(this IServiceCollection services, string baseAddress)
    {
        return services.AddOcelotControlSdk(options => options.BaseAddress = baseAddress);
    }
}