using Gdzie.Kupic.Location;
using Gdzie.Kupic.Location.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Location;

public class ModuleInstallerTests
{
    [Test]
    public void ApiKey_FromGoogleMapsSection_IsSentToGoogle()
    {
        // Same key docker-compose sets via the `GoogleMaps__ApiKey` environment variable.
        var header = ApiKeyHeaderFor(new Dictionary<string, string?> { ["GoogleMaps:ApiKey"] = "compose-key" });

        header.ShouldBe("compose-key");
    }

    [Test]
    public void ApiKey_FromLegacyGoogleOptionsSection_IsStillSupported()
    {
        var header = ApiKeyHeaderFor(new Dictionary<string, string?> { ["GoogleOptions:GeolocationApiKey"] = "legacy-key" });

        header.ShouldBe("legacy-key");
    }

    [Test]
    public void ApiKey_GoogleMapsSection_WinsOverLegacy()
    {
        var header = ApiKeyHeaderFor(new Dictionary<string, string?>
        {
            ["GoogleMaps:ApiKey"] = "compose-key",
            ["GoogleOptions:GeolocationApiKey"] = "legacy-key",
        });

        header.ShouldBe("compose-key");
    }

    private static string ApiKeyHeaderFor(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.InstallLocationModule(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IGoogleGeocodingHttpClient));

        return client.DefaultRequestHeaders.GetValues("X-Goog-Api-Key").Single();
    }
}
