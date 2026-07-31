using System.Diagnostics;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class CurlBattleMetricsPublicPageFetcher(
    BattleMetricsRequestPacer requestPacer,
    IOptions<BattleMetricsWipeFallbackOptions> options)
    : IBattleMetricsPublicPageFetcher
{
    private const int MaximumResponseLength = 5 * 1024 * 1024;
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        + "AppleWebKit/537.36 Chrome/138.0.0.0 Safari/537.36";

    private readonly BattleMetricsWipeFallbackOptions _options = options.Value;

    public async Task<string?> FetchRustSettingsAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        return await FetchAsync(
            $"servers/rust/{battleMetricsServerId}/rust-settings",
            cancellationToken);
    }

    public async Task<string?> SearchRustServersAsync(
        string serverName,
        CancellationToken cancellationToken)
    {
        return await FetchAsync(
            $"servers/rust?q={Uri.EscapeDataString(serverName)}",
            cancellationToken);
    }

    private async Task<string?> FetchAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        await requestPacer.WaitForNextRequestAsync(cancellationToken);

        var executable = OperatingSystem.IsWindows() ? "curl.exe" : "curl";
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--silent");
        startInfo.ArgumentList.Add("--show-error");
        startInfo.ArgumentList.Add("--fail");
        startInfo.ArgumentList.Add("--location");
        startInfo.ArgumentList.Add("--compressed");
        startInfo.ArgumentList.Add("--retry");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("--retry-all-errors");
        startInfo.ArgumentList.Add("--max-time");
        startInfo.ArgumentList.Add(_options.RequestTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--user-agent");
        startInfo.ArgumentList.Add(BrowserUserAgent);
        startInfo.ArgumentList.Add("--header");
        startInfo.ArgumentList.Add("Accept: text/html,application/xhtml+xml");
        startInfo.ArgumentList.Add($"https://www.battlemetrics.com/{relativePath}");

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            await errorTask;

            return process.ExitCode == 0 && output.Length <= MaximumResponseLength
                ? output
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }
        catch (Exception)
        {
            TryKill(process);
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
