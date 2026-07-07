using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class FileBackedRustServerCatalogTests
{
    [Fact]
    public async Task GetConfiguredServersAsync_MergesAppSettingsAndImportedServersByHostAndQueryPort()
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-rust-servers.json");
        await File.WriteAllTextAsync(
            tempFilePath,
            """
            {
              "servers": [
                {
                  "name": "Imported One",
                  "host": "imported.one.test",
                  "queryPort": 28015
                },
                {
                  "name": "Duplicate",
                  "host": "configured.test",
                  "queryPort": 28016
                }
              ]
            }
            """);

        try
        {
            var catalog = new FileBackedRustServerCatalog(
                Options.Create(new RustServerQueryOptions
                {
                    Servers =
                    [
                        new ConfiguredRustServer
                        {
                            Name = "Configured",
                            Host = "configured.test",
                            QueryPort = 28016
                        }
                    ]
                }),
                Options.Create(new RustServerCatalogImportOptions
                {
                    Enabled = true,
                    JsonFilePath = tempFilePath
                }));

            var servers = await catalog.GetConfiguredServersAsync(CancellationToken.None);

            Assert.Equal(2, servers.Count);
            Assert.Contains(servers, server => server.Host == "configured.test" && server.QueryPort == 28016);
            Assert.Contains(servers, server => server.Host == "imported.one.test" && server.QueryPort == 28015);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    [Fact]
    public async Task GetConfiguredServersAsync_ThrowsWhenImportIsEnabledAndFileIsMissing()
    {
        var catalog = new FileBackedRustServerCatalog(
            Options.Create(new RustServerQueryOptions()),
            Options.Create(new RustServerCatalogImportOptions
            {
                Enabled = true,
                JsonFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-missing.json")
            }));

        await Assert.ThrowsAsync<FileNotFoundException>(() => catalog.GetConfiguredServersAsync(CancellationToken.None));
    }
}
