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
        // `GoogleMaps:ApiKey` is what docker-compose and the docs use (env var `GoogleMaps__ApiKey`);
        // the older `GoogleOptions:GeolocationApiKey` is still honoured.
        var legacyOptions = new GoogleOptions();
        configuration.Bind("GoogleOptions", legacyOptions);
        var apiKey = configuration["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = legacyOptions.GeolocationApiKey;
        }

        services.AddHttpClient<IGoogleGeocodingHttpClient, GoogleGeocodingHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://geocode.googleapis.com/v4/");
            client.DefaultRequestHeaders.Add("X-Goog-Api-Key", apiKey);
        });

        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ILocationInputResolver, LocationInputResolver>();
        services.AddScoped<ISavedLocationService, SavedLocationService>();
    }
}