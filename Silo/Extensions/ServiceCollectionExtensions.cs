// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Stripe;

namespace Microsoft.Extensions.DependencyInjection;

internal static class ServiceCollectionExtensions
{
    internal static void AddApplicationInsights(
        this IServiceCollection services, string applicationName)
    {
        services.AddApplicationInsightsTelemetry();
        services.AddSingleton<ITelemetryInitializer>(
            _ => new ApplicationMapNodeNameInitializer(applicationName));
    }

    public static IServiceCollection AddStripe(this IServiceCollection services, IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["SECRET_KEY"];

        var appInfo = new AppInfo
        {
            Name = "OrleansShoppingCart",
            Version = "0.1.0"
        };
        StripeConfiguration.AppInfo = appInfo;

        services.AddHttpClient("Stripe");
        services.AddTransient<IStripeClient, StripeClient>(s =>
        {
            var clientFactory = s.GetRequiredService<IHttpClientFactory>();
            var httpClient = new SystemNetHttpClient(
               httpClient: clientFactory.CreateClient("Stripe"),
               maxNetworkRetries: StripeConfiguration.MaxNetworkRetries,
               appInfo: appInfo,
               enableTelemetry: StripeConfiguration.EnableTelemetry);

            return new StripeClient(apiKey: StripeConfiguration.ApiKey, httpClient: httpClient);
        });

        return services;
    }
}
