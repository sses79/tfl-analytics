using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Infrastructure.Tfl;

namespace TflAnalytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TflApiOptions>()
            .Bind(configuration.GetSection(TflApiOptions.SectionName));

        services.AddHttpClient<ITflApiClient, TflApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<TflApiOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
        });

        return services;
    }
}
