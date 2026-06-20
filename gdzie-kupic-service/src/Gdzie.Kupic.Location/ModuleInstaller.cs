namespace Gdzie.Kupic.Location;

using Gdzie.Kupic.Location.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal sealed class GoogleOptions
{
    public string GeolocationApiKey { get; init; }
}

public static class ModuleInstaller
{
    public static void InstallLocationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var googleOptions = new GoogleOptions();
        configuration.Bind("GoogleOptions", googleOptions);

        services.AddHttpClient<IGoogleGeocodingHttpClient, GoogleGeocodingHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://geocode.googleapis.com/v4/");
            client.DefaultRequestHeaders.Add("X-Goog-Api-Key", googleOptions.GeolocationApiKey);
        });

        services.AddScoped<ILocationService, LocationService>();
    }
}