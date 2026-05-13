using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.Deployment.DotNet.Releases;

namespace Org.Sdk.Testing;

/// <summary>
/// Helper for downloading and caching .NET SDK versions for testing.
/// </summary>
public static class DotNetSdkHelpers
{
    private static readonly ConcurrentDictionary<NetSdkVersion, string> Values = new();
    private static readonly SemaphoreSlim Lock = new(1, 1);

    /// <summary>
    /// Gets the path to the dotnet executable for the specified SDK version.
    /// Downloads and caches the SDK if not already available.
    /// </summary>
    public static async Task<string> Get(NetSdkVersion version)
    {
        if (Values.TryGetValue(version, out var result))
            return result;

        await Lock.WaitAsync();
        try
        {
            if (Values.TryGetValue(version, out result))
                return result;

            var versionString = version switch
            {
                NetSdkVersion.Net10_0 => "10.0",
                NetSdkVersion.Net11_0 => "11.0",
                _ => throw new NotSupportedException($"SDK version {version} is not supported"),
            };

            var products = await ProductCollection.GetAsync();
            var product = products.Single(a => a.ProductName == ".NET" && a.ProductVersion == versionString);
            var releases = await product.GetReleasesAsync();
            var latestRelease = releases.Single(r => r.Version == product.LatestReleaseVersion);
            var latestSdk = latestRelease.Sdks.MaxBy(sdk => sdk.Version);

            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            var expectedExtension = OperatingSystem.IsWindows() ? ".zip" : ".gz";
            var file = latestSdk!.Files.Single(file => file.Rid == runtimeIdentifier && Path.GetExtension(file.Name) == expectedExtension);

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var finalFolderPath = Path.Combine(localAppData, "org-sdk", "dotnet", latestSdk.Version.ToString());
            var dotnetExecutable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            var finalDotnetPath = Path.Combine(finalFolderPath, dotnetExecutable);

            if (File.Exists(finalDotnetPath))
            {
                Values[version] = finalDotnetPath;
                return finalDotnetPath;
            }

            var tempFolder = Path.Combine(Path.GetTempPath(), "dotnet", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            var bytes = await SharedHttpClient.Instance.GetByteArrayAsync(file.Address);
            if (Path.GetExtension(file.Name) is ".zip")
            {
                using var ms = new MemoryStream(bytes);
                var zip = new ZipArchive(ms);
                await zip.ExtractToDirectoryAsync(tempFolder, overwriteFiles: true);
            }
            else
            {
                // .tar.gz
                try
                {
                    using var ms = new MemoryStream(bytes);
                    using var gzipStream = new GZipStream(ms, CompressionMode.Decompress);
                    await TarFile.ExtractToDirectoryAsync(gzipStream, tempFolder, overwriteFiles: true);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to extract SDK archive", ex);
                }
            }

            var tempDotnetPath = Path.Combine(tempFolder, dotnetExecutable);
            if (!File.Exists(tempDotnetPath))
                throw new InvalidOperationException($"The extracted SDK archive does not contain '{dotnetExecutable}' in '{tempFolder}'");

            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("Updating permissions of " + tempDotnetPath);
                File.SetUnixFileMode(tempDotnetPath, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

                foreach (var cscPath in Directory.GetFiles(tempFolder, "csc", SearchOption.AllDirectories))
                {
                    Console.WriteLine("Updating permissions of " + cscPath);
                    File.SetUnixFileMode(cscPath, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }

            try
            {
                var parentDir = Path.GetDirectoryName(finalFolderPath);
                if (parentDir != null)
                    Directory.CreateDirectory(parentDir);
                Directory.Move(tempFolder, finalFolderPath);
            }
            catch
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, recursive: true);
                }
            }

            if (!File.Exists(finalDotnetPath))
                throw new InvalidOperationException($"Failed to install SDK to '{finalDotnetPath}'");

            Values[version] = finalDotnetPath;
            return finalDotnetPath;
        }
        finally
        {
            Lock.Release();
        }
    }
}

/// <summary>
/// Shared HTTP client with retry logic for transient failures.
/// </summary>
file static class SharedHttpClient
{
    public static HttpClient Instance { get; } = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var socketHandler = new SocketsHttpHandler()
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        };

        return new HttpClient(new HttpRetryMessageHandler(socketHandler), disposeHandler: true);
    }

    private sealed class HttpRetryMessageHandler(HttpMessageHandler handler) : DelegatingHandler(handler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const int maxRetries = 5;
            var defaultDelay = TimeSpan.FromMilliseconds(200);
            for (var i = 1; ; i++, defaultDelay *= 2)
            {
                TimeSpan? delayHint = null;
                HttpResponseMessage? result = null;

                try
                {
                    result = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!IsLastAttempt(i) && ((int)result.StatusCode >= 500 || result.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests))
                    {
                        // Use "Retry-After" value, if available. Typically, this is sent with
                        // either a 503 (Service Unavailable) or 429 (Too Many Requests):
                        // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Retry-After

                        delayHint = result.Headers.RetryAfter switch
                        {
                            { Date: { } date } => date - DateTimeOffset.UtcNow,
                            { Delta: { } delta } => delta,
                            _ => null,
                        };

                        result.Dispose();
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (HttpRequestException)
                {
                    result?.Dispose();
                    if (IsLastAttempt(i))
                        throw;
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken) // catch "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing"
                {
                    result?.Dispose();
                    if (IsLastAttempt(i))
                        throw;
                }

                await Task.Delay(delayHint is { } someDelay && someDelay > TimeSpan.Zero ? someDelay : defaultDelay, cancellationToken).ConfigureAwait(false);

                static bool IsLastAttempt(int i) => i >= maxRetries;
            }
        }
    }
}
