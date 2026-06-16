using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BluetoothBatteryWidget.App.Services;

public sealed class UpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/samueltoken/Bloss_battery_indicator/releases/latest";
    private const string ExpectedAssetName = "setup.exe";
    private const string ExpectedChecksumAssetName = "setup.exe.sha256";
    private const long MaxInstallerBytes = 250L * 1024 * 1024;
    private const int MaxChecksumBytes = 16 * 1024;

    private static readonly string[] TrustedDownloadHosts =
    [
        "github.com",
        "objects.githubusercontent.com",
        "github-releases.githubusercontent.com",
        "release-assets.githubusercontent.com"
    ];

    private readonly HttpClient _httpClient;
    private readonly string _appDisplayName;
    private readonly Func<string> _displayVersionProvider;

    public UpdateService(
        HttpClient httpClient,
        string appDisplayName,
        Func<string> displayVersionProvider)
    {
        _httpClient = httpClient;
        _appDisplayName = appDisplayName;
        _displayVersionProvider = displayVersionProvider;
    }

    public async Task<(UpdateReleaseAssetInfo? Release, string? ErrorMessage)> TryGetLatestReleaseAssetAsync(
        UpdateServiceText text)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appDisplayName, _displayVersionProvider()));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return (null, text.ReleaseReadFailed);
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagNameElement))
            {
                return (null, text.ReleaseReadFailed);
            }

            if (root.TryGetProperty("draft", out var draftElement) &&
                draftElement.ValueKind is JsonValueKind.True &&
                draftElement.GetBoolean())
            {
                return (null, text.ReleaseReadFailed);
            }

            if (root.TryGetProperty("prerelease", out var prereleaseElement) &&
                prereleaseElement.ValueKind is JsonValueKind.True &&
                prereleaseElement.GetBoolean())
            {
                return (null, text.ReleaseReadFailed);
            }

            var latestVersion = NormalizeReleaseVersion(tagNameElement.GetString());
            if (!root.TryGetProperty("assets", out var assetsElement) ||
                assetsElement.ValueKind != JsonValueKind.Array)
            {
                return (null, text.AssetMissing);
            }

            string? setupDownloadUrl = null;
            string? checksumDownloadUrl = null;
            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    continue;
                }

                var assetName = nameElement.GetString();
                var downloadUrl = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(downloadUrl))
                {
                    continue;
                }

                if (string.Equals(assetName, ExpectedAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    setupDownloadUrl = downloadUrl;
                    continue;
                }

                if (string.Equals(assetName, ExpectedChecksumAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    checksumDownloadUrl = downloadUrl;
                }
            }

            if (string.IsNullOrWhiteSpace(setupDownloadUrl))
            {
                return (null, text.AssetMissing);
            }

            if (string.IsNullOrWhiteSpace(checksumDownloadUrl))
            {
                return (null, text.ChecksumMissing);
            }

            if (!IsTrustedDownloadUrl(setupDownloadUrl) ||
                !IsTrustedDownloadUrl(checksumDownloadUrl))
            {
                return (null, text.SourceNotTrusted);
            }

            return (new UpdateReleaseAssetInfo(latestVersion, setupDownloadUrl, checksumDownloadUrl), null);
        }
        catch
        {
            return (null, text.ReleaseReadFailed);
        }
    }

    public async Task<string> DownloadAndVerifyInstallerAsync(
        UpdateReleaseAssetInfo releaseInfo,
        UpdateServiceText text,
        Action<UpdateProgress>? progress)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Bloss", "updates");
        Directory.CreateDirectory(tempRoot);
        var setupPath = Path.Combine(
            tempRoot,
            $"setup-{releaseInfo.Version}-{DateTime.UtcNow:yyyyMMddHHmmss}.exe");

        await DownloadInstallerAsync(releaseInfo.SetupDownloadUrl, setupPath, text, progress);

        progress?.Invoke(new UpdateProgress(text.Verifying, 100, IsIndeterminate: true));
        var checksumContent = await DownloadChecksumAsync(releaseInfo.ChecksumDownloadUrl, text);
        if (!TryExtractSha256Hash(checksumContent, out var expectedHash))
        {
            TryDeleteFile(setupPath);
            throw new InvalidOperationException(text.VerificationFailed);
        }

        var downloadedHash = ComputeFileSha256(setupPath);
        if (!string.Equals(downloadedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(setupPath);
            throw new InvalidOperationException(text.VerificationFailed);
        }

        return setupPath;
    }

    public void StartInstallerUpdateAndRestart(
        string setupPath,
        string targetVersion,
        string fallbackVersion,
        string installLaunchFailedMessage)
    {
        if (!File.Exists(setupPath))
        {
            throw new FileNotFoundException("Downloaded setup file was not found.", setupPath);
        }

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            targetVersion = fallbackVersion;
        }

        var currentProcessPath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            currentProcessPath = Path.Combine(AppContext.BaseDirectory, "Bloss.exe");
        }

        if (string.IsNullOrWhiteSpace(currentProcessPath) || !File.Exists(currentProcessPath))
        {
            throw new FileNotFoundException(installLaunchFailedMessage);
        }

        var updateLogDirectory = Path.Combine(Path.GetTempPath(), "Bloss", "updates");
        Directory.CreateDirectory(updateLogDirectory);

        var installLogPath = Path.Combine(
            updateLogDirectory,
            $"install-{GetSafeUpdateLogFileNamePart(targetVersion, fallbackVersion)}-{DateTime.UtcNow:yyyyMMddHHmmss}.log");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"bloss-updater-{Guid.NewGuid():N}.ps1");
        var escapedSetupPath = EscapePowerShellSingleQuotedString(setupPath);
        var escapedAppPath = EscapePowerShellSingleQuotedString(currentProcessPath);
        var escapedTargetVersion = EscapePowerShellSingleQuotedString(targetVersion);
        var escapedLogPath = EscapePowerShellSingleQuotedString(installLogPath);
        var currentPid = Environment.ProcessId;

        var scriptBuilder = new StringBuilder();
        scriptBuilder.AppendLine("$ErrorActionPreference = 'Continue'");
        scriptBuilder.AppendLine($"$setupPath = '{escapedSetupPath}'");
        scriptBuilder.AppendLine($"$appPath = '{escapedAppPath}'");
        scriptBuilder.AppendLine($"$targetVersion = '{escapedTargetVersion}'");
        scriptBuilder.AppendLine($"$logPath = '{escapedLogPath}'");
        scriptBuilder.AppendLine($"$oldPid = {currentPid}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("function Write-BlossUpdateLog {");
        scriptBuilder.AppendLine("    param([string]$message)");
        scriptBuilder.AppendLine("    try {");
        scriptBuilder.AppendLine("        New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null");
        scriptBuilder.AppendLine("        Add-Content -LiteralPath $logPath -Value $message -Encoding UTF8");
        scriptBuilder.AppendLine("    } catch {");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("function Test-BlossFileVersion {");
        scriptBuilder.AppendLine("    param([string]$filePath, [string]$versionPrefix)");
        scriptBuilder.AppendLine("    if (-not (Test-Path -LiteralPath $filePath)) { return $false }");
        scriptBuilder.AppendLine("    try {");
        scriptBuilder.AppendLine("        $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($filePath)");
        scriptBuilder.AppendLine("        return (($info.ProductVersion -like ($versionPrefix + '*')) -or ($info.FileVersion -like ($versionPrefix + '*')))");
        scriptBuilder.AppendLine("    } catch {");
        scriptBuilder.AppendLine("        return $false");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("function Test-BlossInstalledVersion {");
        scriptBuilder.AppendLine("    param([string]$versionPrefix)");
        scriptBuilder.AppendLine("    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique");
        scriptBuilder.AppendLine("    foreach ($root in $roots) {");
        scriptBuilder.AppendLine("        $installDir = Join-Path $root 'Bloss'");
        scriptBuilder.AppendLine("        $exePath = Join-Path $installDir 'Bloss.exe'");
        scriptBuilder.AppendLine("        $dllPath = Join-Path $installDir 'Bloss.dll'");
        scriptBuilder.AppendLine("        if ((Test-BlossFileVersion $exePath $versionPrefix) -and (Test-BlossFileVersion $dllPath $versionPrefix)) {");
        scriptBuilder.AppendLine("            return $true");
        scriptBuilder.AppendLine("        }");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine("    return $false");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("Write-BlossUpdateLog ('update_start=' + (Get-Date).ToString('o'))");
        scriptBuilder.AppendLine("Write-BlossUpdateLog ('target_version=' + $targetVersion)");
        scriptBuilder.AppendLine("Write-BlossUpdateLog ('setup_path=' + $setupPath)");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("while (Get-Process -Id $oldPid -ErrorAction SilentlyContinue) {");
        scriptBuilder.AppendLine("    Start-Sleep -Milliseconds 600");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("$installArgs = @(");
        scriptBuilder.AppendLine("    '/VERYSILENT',");
        scriptBuilder.AppendLine("    '/SUPPRESSMSGBOXES',");
        scriptBuilder.AppendLine("    '/NORESTART',");
        scriptBuilder.AppendLine("    '/NORESTARTAPPLICATIONS',");
        scriptBuilder.AppendLine("    '/CLOSEAPPLICATIONS',");
        scriptBuilder.AppendLine("    '/SP-',");
        scriptBuilder.AppendLine("    ('/LOG=\"' + $logPath + '\"')");
        scriptBuilder.AppendLine(")");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("$installExitCode = $null");
        scriptBuilder.AppendLine("try {");
        scriptBuilder.AppendLine("    $process = Start-Process -FilePath $setupPath -ArgumentList $installArgs -Verb RunAs -Wait -PassThru");
        scriptBuilder.AppendLine("    if ($null -ne $process) { $installExitCode = $process.ExitCode }");
        scriptBuilder.AppendLine("    Write-BlossUpdateLog ('installer_exit_code=' + $installExitCode)");
        scriptBuilder.AppendLine("} catch {");
        scriptBuilder.AppendLine("    Write-BlossUpdateLog ('installer_launch_error=' + $_.Exception.Message)");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("$installed = $false");
        scriptBuilder.AppendLine("$deadline = (Get-Date).AddSeconds(120)");
        scriptBuilder.AppendLine("while ((Get-Date) -lt $deadline) {");
        scriptBuilder.AppendLine("    if (Test-BlossInstalledVersion $targetVersion) {");
        scriptBuilder.AppendLine("        $installed = $true");
        scriptBuilder.AppendLine("        break");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine("    Start-Sleep -Milliseconds 500");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine("Write-BlossUpdateLog ('installed_target_version=' + $installed)");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("if (Test-Path -LiteralPath $appPath) {");
        scriptBuilder.AppendLine("    Start-Process -FilePath $appPath");
        scriptBuilder.AppendLine("}");
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue");
        var scriptContent = scriptBuilder.ToString();

        File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    public static bool IsRemoteVersionNewer(string currentVersionText, string remoteVersionText)
    {
        if (TryParseComparableVersion(currentVersionText, out var currentVersion) &&
            TryParseComparableVersion(remoteVersionText, out var remoteVersion))
        {
            return remoteVersion > currentVersion;
        }

        return !string.Equals(
            NormalizeReleaseVersion(currentVersionText),
            NormalizeReleaseVersion(remoteVersionText),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseComparableVersion(string? rawVersion, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return false;
        }

        var normalized = NormalizeReleaseVersion(rawVersion);
        var separatorIndex = normalized.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            normalized = normalized[..separatorIndex];
        }

        var parsed = Version.TryParse(normalized, out var parsedVersion);
        version = parsedVersion ?? new Version(0, 0, 0, 0);
        return parsed;
    }

    public static string NormalizeReleaseVersion(string? rawVersion)
    {
        var text = rawVersion?.Trim() ?? string.Empty;
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return text.Trim();
    }

    public static bool IsTrustedDownloadUrl(string downloadUrl)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var trustedHost in TrustedDownloadHosts)
        {
            if (string.Equals(uri.Host, trustedHost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryExtractSha256Hash(string checksumContent, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(checksumContent))
        {
            return false;
        }

        foreach (var rawLine in checksumContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var hashCandidate = line;
            var separator = line.IndexOfAny([' ', '\t']);
            if (separator > 0)
            {
                hashCandidate = line[..separator];
            }

            if (hashCandidate.Length != 64)
            {
                continue;
            }

            var allHex = true;
            for (var i = 0; i < hashCandidate.Length; i++)
            {
                var ch = hashCandidate[i];
                var isHex = (ch >= '0' && ch <= '9') ||
                            (ch >= 'a' && ch <= 'f') ||
                            (ch >= 'A' && ch <= 'F');
                if (!isHex)
                {
                    allHex = false;
                    break;
                }
            }

            if (!allHex)
            {
                continue;
            }

            hash = hashCandidate.ToUpperInvariant();
            return true;
        }

        return false;
    }

    public static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private async Task DownloadInstallerAsync(
        string downloadUrl,
        string destinationPath,
        UpdateServiceText text,
        Action<UpdateProgress>? progress)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appDisplayName, _displayVersionProvider()));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            if (totalBytes is > MaxInstallerBytes)
            {
                throw new InvalidOperationException(text.VerificationFailed);
            }

            var downloadedBytes = 0L;

            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read <= 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read));
                downloadedBytes += read;
                if (downloadedBytes > MaxInstallerBytes)
                {
                    throw new InvalidOperationException(text.VerificationFailed);
                }

                if (totalBytes is > 0)
                {
                    var percent = Math.Clamp(downloadedBytes * 100d / totalBytes.Value, 0d, 100d);
                    var message = string.Format(text.DownloadingFormat, Math.Round(percent, 0));
                    progress?.Invoke(new UpdateProgress(message, percent, IsIndeterminate: false));
                }
                else
                {
                    progress?.Invoke(new UpdateProgress(text.Downloading, 0, IsIndeterminate: true));
                }
            }
        }
        catch
        {
            TryDeleteFile(destinationPath);
            throw;
        }
    }

    private async Task<string> DownloadChecksumAsync(string downloadUrl, UpdateServiceText text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(_appDisplayName, _displayVersionProvider()));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxChecksumBytes)
        {
            throw new InvalidOperationException(text.VerificationFailed);
        }

        await using var source = await response.Content.ReadAsStreamAsync();
        using var memory = new MemoryStream();
        var buffer = new byte[2048];

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read <= 0)
            {
                break;
            }

            await memory.WriteAsync(buffer.AsMemory(0, read));
            if (memory.Length > MaxChecksumBytes)
            {
                throw new InvalidOperationException(text.VerificationFailed);
            }
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static string EscapePowerShellSingleQuotedString(string path)
    {
        return path.Replace("'", "''");
    }

    private static string GetSafeUpdateLogFileNamePart(string value, string fallbackVersion)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_');
        }

        return builder.Length > 0 ? builder.ToString() : fallbackVersion;
    }
}

public sealed record UpdateReleaseAssetInfo(
    string Version,
    string SetupDownloadUrl,
    string ChecksumDownloadUrl);

public sealed record UpdateProgress(
    string Message,
    double ProgressPercent,
    bool IsIndeterminate);

public sealed record UpdateServiceText(
    string AssetMissing,
    string ReleaseReadFailed,
    string ChecksumMissing,
    string SourceNotTrusted,
    string Downloading,
    string DownloadingFormat,
    string Verifying,
    string VerificationFailed);
