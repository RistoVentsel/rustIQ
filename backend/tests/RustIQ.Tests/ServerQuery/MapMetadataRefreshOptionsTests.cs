using Microsoft.Extensions.Configuration;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class MapMetadataRefreshOptionsTests
{
    [Fact]
    public void ConfigurationBinding_DoesNotDuplicateConfiguredLocalHours()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MapMetadataRefresh:LocalHours:0"] = "3",
                ["MapMetadataRefresh:LocalHours:1"] = "18",
                ["MapMetadataRefresh:LocalHours:2"] = "22"
            })
            .Build();

        var options = configuration
            .GetSection(MapMetadataRefreshOptions.SectionName)
            .Get<MapMetadataRefreshOptions>();

        Assert.NotNull(options);
        Assert.Equal([3, 18, 22], options.LocalHours);
    }
}
