using System.Net;
using System.Text;
using BluetoothBatteryWidget.App.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.0.7", "1.0.7")]
    [InlineData("  V1.0.7  ", "1.0.7")]
    [InlineData("1.0.7", "1.0.7")]
    public void NormalizeReleaseVersion_RemovesTagPrefixWithoutAddingFourthPart(string raw, string expected)
    {
        Assert.Equal(expected, UpdateService.NormalizeReleaseVersion(raw));
    }

    [Theory]
    [InlineData("1.0.6", "1.0.7", true)]
    [InlineData("1.0.7", "1.0.7", false)]
    [InlineData("v1.0.7", "v1.0.9", true)]
    public void IsRemoteVersionNewer_ComparesReleaseVersions(string current, string remote, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsRemoteVersionNewer(current, remote));
    }

    [Theory]
    [InlineData("https://github.com/owner/repo/releases/download/v1.0.7/setup.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset/setup.exe", true)]
    [InlineData("http://github.com/owner/repo/releases/download/v1.0.7/setup.exe", false)]
    [InlineData("https://example.com/setup.exe", false)]
    [InlineData("not a url", false)]
    public void IsTrustedDownloadUrl_AllowsOnlyKnownHttpsReleaseHosts(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsTrustedDownloadUrl(url));
    }

    [Fact]
    public void TryExtractSha256Hash_ReadsFirstValidHash()
    {
        var expected = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
        var content = $"""
            # setup.exe sha256
            {expected.ToLowerInvariant()}  setup.exe
            """;

        Assert.True(UpdateService.TryExtractSha256Hash(content, out var hash));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeFileSha256_ReturnsUppercaseHash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bloss-hash-test-{Guid.NewGuid():N}.bin");

        try
        {
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("abc"));

            var hash = UpdateService.ComputeFileSha256(path);

            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task TryGetLatestReleaseAssetAsync_ReturnsExpectedSetupAndChecksumAssets()
    {
        using var httpClient = new HttpClient(new StaticJsonHandler("""
            {
              "tag_name": "v1.0.7",
              "draft": false,
              "prerelease": false,
              "assets": [
                {
                  "name": "setup.exe.sha256",
                  "browser_download_url": "https://github.com/samueltoken/Bloss_battery_indicator/releases/download/v1.0.7/setup.exe.sha256"
                },
                {
                  "name": "setup.exe",
                  "browser_download_url": "https://github.com/samueltoken/Bloss_battery_indicator/releases/download/v1.0.7/setup.exe"
                }
              ]
            }
            """));
        var service = new UpdateService(httpClient, "Bloss", () => "1.0.7");

        var (release, error) = await service.TryGetLatestReleaseAssetAsync(BuildText());

        Assert.Null(error);
        Assert.NotNull(release);
        Assert.Equal("1.0.7", release.Version);
        Assert.EndsWith("/setup.exe", release.SetupDownloadUrl, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/setup.exe.sha256", release.ChecksumDownloadUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static UpdateServiceText BuildText()
    {
        return new UpdateServiceText(
            "asset missing",
            "release read failed",
            "checksum missing",
            "source not trusted",
            "downloading",
            "downloading {0}%",
            "verifying",
            "verification failed");
    }

    private sealed class StaticJsonHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StaticJsonHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
