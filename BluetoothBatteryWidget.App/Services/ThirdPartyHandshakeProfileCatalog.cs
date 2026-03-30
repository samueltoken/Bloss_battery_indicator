using BluetoothBatteryWidget.Core.Services;
using System.IO;
using System.Text.Json;

namespace BluetoothBatteryWidget.App.Services;

internal static class ThirdPartyHandshakeProfileCatalog
{
    private const string DefaultProfileId = "generic.default";
    private const string XboxLayerProfileId = "xbox.layer";
    private const string GuliKitProfileId = "brand.gulikit";
    private const string FlydigiProfileId = "brand.flydigi";
    private const string EasySmxProfileId = "brand.easysmx";
    private const string GameSirProfileId = "brand.gamesir";
    private static readonly string OverrideFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bloss",
        "brand-handshake-overrides.json");
    private static readonly JsonSerializerOptions OverrideJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly object OverrideSync = new();
    private static DateTime _overrideLastWriteUtc = DateTime.MinValue;
    private static List<ExternalOverrideRule> _cachedExternalRules = [];

    private static readonly ThirdPartyInitPacket[] CommonInitPackets =
    [
        new([0x05, 0x20, 0x00, 0x01, 0x00]),
        new([0x0A, 0x20, 0x00, 0x03, 0x00, 0x01, 0x14]),
        new([0x06, 0x20, 0x00, 0x02, 0x01, 0x00]),
        new([0x09, 0x00, 0x00, 0x09, 0x00, 0x0F, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0xEB], 10)
    ];

    private static readonly ThirdPartyHandshakeProfile DefaultProfile = new(
        ProfileId: DefaultProfileId,
        InitPackets: CommonInitPackets,
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82],
        PreferredInputReportIds: [0x04, 0x01, 0x11, 0x21, 0x31, 0x81, 0x82],
        RecoveryInputReportIds: [0x01, 0x04, 0x03, 0x11, 0x21, 0x31, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly ThirdPartyHandshakeProfile XboxLayerProfile = new(
        ProfileId: XboxLayerProfileId,
        InitPackets:
        [
            new ThirdPartyInitPacket([0x05, 0x20, 0x00, 0x01, 0x00]),
            new ThirdPartyInitPacket([0x0A, 0x20, 0x00, 0x03, 0x00, 0x01, 0x14]),
            new ThirdPartyInitPacket([0x06, 0x20, 0x00, 0x02, 0x01, 0x00])
        ],
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81],
        PreferredInputReportIds: [0x04, 0x01, 0x11, 0x21, 0x31, 0x81, 0x82],
        RecoveryInputReportIds: [0x04, 0x01, 0x03, 0x11, 0x21, 0x31, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly ThirdPartyHandshakeProfile GuliKitProfile = new(
        ProfileId: GuliKitProfileId,
        InitPackets:
        [
            new ThirdPartyInitPacket([0x05, 0x20, 0x00, 0x01, 0x00]),
            new ThirdPartyInitPacket([0x0A, 0x20, 0x00, 0x03, 0x00, 0x01, 0x14]),
            new ThirdPartyInitPacket([0x06, 0x20, 0x00, 0x02, 0x01, 0x00]),
            new ThirdPartyInitPacket([0x08, 0x20, 0x00, 0x04, 0x01, 0x00], 8)
        ],
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82],
        PreferredInputReportIds: [0x04, 0x01, 0x03, 0x21, 0x31, 0x11, 0x81, 0x82],
        RecoveryInputReportIds: [0x01, 0x03, 0x04, 0x21, 0x31, 0x11, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly ThirdPartyHandshakeProfile FlydigiProfile = new(
        ProfileId: FlydigiProfileId,
        InitPackets:
        [
            new ThirdPartyInitPacket([0x05, 0x20, 0x00, 0x01, 0x00]),
            new ThirdPartyInitPacket([0x0A, 0x20, 0x00, 0x03, 0x00, 0x01, 0x14]),
            new ThirdPartyInitPacket([0x0C, 0x20, 0x00, 0x05, 0x00, 0x00], 8)
        ],
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82],
        PreferredInputReportIds: [0x21, 0x31, 0x11, 0x01, 0x04, 0x03, 0x81, 0x82],
        RecoveryInputReportIds: [0x21, 0x31, 0x11, 0x01, 0x03, 0x04, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly ThirdPartyHandshakeProfile EasySmxProfile = new(
        ProfileId: EasySmxProfileId,
        InitPackets:
        [
            new ThirdPartyInitPacket([0x05, 0x20, 0x00, 0x01, 0x00]),
            new ThirdPartyInitPacket([0x0A, 0x20, 0x00, 0x03, 0x00, 0x01, 0x14]),
            new ThirdPartyInitPacket([0x09, 0x00, 0x00, 0x09, 0x00, 0x0F, 0x00, 0x00, 0x1D, 0x1D, 0xFF, 0x00, 0x00], 10)
        ],
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x12, 0x21, 0x31, 0x81, 0x82],
        PreferredInputReportIds: [0x04, 0x01, 0x11, 0x12, 0x21, 0x31, 0x81, 0x82],
        RecoveryInputReportIds: [0x04, 0x01, 0x11, 0x12, 0x21, 0x31, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly ThirdPartyHandshakeProfile GameSirProfile = new(
        ProfileId: GameSirProfileId,
        InitPackets: CommonInitPackets,
        FeatureReportIds: [0x02, 0x03, 0x05, 0x11, 0x21, 0x31, 0x81, 0x82],
        PreferredInputReportIds: [0x04, 0x11, 0x21, 0x31, 0x01, 0x81, 0x82],
        RecoveryInputReportIds: [0x04, 0x01, 0x11, 0x21, 0x31, 0x30, 0x32, 0x81, 0x82],
        MinimumReportSize: 64);

    private static readonly IReadOnlyDictionary<string, ThirdPartyHandshakeProfile> Profiles =
        new Dictionary<string, ThirdPartyHandshakeProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [GamepadProfileStore.BuildModelKey("2DC8", "6100")] = XboxLayerProfile,
            [GamepadProfileStore.BuildModelKey("045E", "0B13")] = XboxLayerProfile,
            [GamepadProfileStore.BuildModelKey("24C6", "541A")] = FlydigiProfile,
            [GamepadProfileStore.BuildModelKey("20BC", "5500")] = GameSirProfile,
            [GamepadProfileStore.BuildModelKey("20BC", "5501")] = GameSirProfile,
            [GamepadProfileStore.BuildModelKey("20BC", "5502")] = GameSirProfile
        };

    private static readonly IReadOnlyDictionary<string, ThirdPartyHandshakeProfile> ProfilesByBrand =
        new Dictionary<string, ThirdPartyHandshakeProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["gulikit"] = GuliKitProfile,
            ["flydigi"] = FlydigiProfile,
            ["easysmx"] = EasySmxProfile,
            ["gamesir"] = GameSirProfile,
            ["xbox"] = XboxLayerProfile
        };

    private static readonly IReadOnlyDictionary<string, ThirdPartyHandshakeProfile> ProfilesById =
        new Dictionary<string, ThirdPartyHandshakeProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultProfileId] = DefaultProfile,
            [XboxLayerProfileId] = XboxLayerProfile,
            [GuliKitProfileId] = GuliKitProfile,
            [FlydigiProfileId] = FlydigiProfile,
            [EasySmxProfileId] = EasySmxProfile,
            [GameSirProfileId] = GameSirProfile
        };

    public static ThirdPartyHandshakeProfile Resolve(string? vendorId, string? productId)
    {
        return Resolve(vendorId, productId, displayName: null, endpointSignal: null, deviceAddress: null);
    }

    public static ThirdPartyHandshakeProfile Resolve(
        string? vendorId,
        string? productId,
        string? displayName,
        string? endpointSignal,
        string? deviceAddress = null)
    {
        return ResolveSelection(vendorId, productId, displayName, endpointSignal, deviceAddress).Profile;
    }

    public static ThirdPartyHandshakeSelection ResolveSelection(
        string? vendorId,
        string? productId,
        string? displayName,
        string? endpointSignal,
        string? deviceAddress)
    {
        if (TryResolveExternalOverride(
                vendorId,
                productId,
                displayName,
                endpointSignal,
                deviceAddress,
                out var overrideSelection))
        {
            return overrideSelection;
        }

        var (brandHint, reason) = ResolveBrandHint(
            displayName,
            endpointSignal,
            vendorId,
            productId,
            deviceAddress);
        if (!string.IsNullOrWhiteSpace(brandHint) &&
            ProfilesByBrand.TryGetValue(brandHint, out var profileByBrand))
        {
            return new ThirdPartyHandshakeSelection(profileByBrand, brandHint, $"brand:{reason}");
        }

        var modelKey = BatteryModelKeyResolver.ResolveFromVidPid(vendorId, productId);
        if (!string.IsNullOrWhiteSpace(modelKey) && Profiles.TryGetValue(modelKey, out var profile))
        {
            return new ThirdPartyHandshakeSelection(profile, string.Empty, $"model:{modelKey}");
        }

        var signalModelKey = BatteryModelKeyResolver.ResolveFromSignal(endpointSignal);
        if (!string.IsNullOrWhiteSpace(signalModelKey) && Profiles.TryGetValue(signalModelKey, out profile))
        {
            return new ThirdPartyHandshakeSelection(profile, string.Empty, $"signal:{signalModelKey}");
        }

        return new ThirdPartyHandshakeSelection(DefaultProfile, string.Empty, "fallback:default");
    }

    private static bool TryResolveExternalOverride(
        string? vendorId,
        string? productId,
        string? displayName,
        string? endpointSignal,
        string? deviceAddress,
        out ThirdPartyHandshakeSelection selection)
    {
        var rules = LoadExternalOverrideRules();
        if (rules.Count == 0)
        {
            selection = default!;
            return false;
        }

        var normalizedVendor = string.IsNullOrWhiteSpace(vendorId)
            ? string.Empty
            : vendorId.Trim().ToUpperInvariant();
        var normalizedProduct = string.IsNullOrWhiteSpace(productId)
            ? string.Empty
            : productId.Trim().ToUpperInvariant();
        var normalizedAddress = AddressNormalizer.NormalizeAddress(deviceAddress);
        var normalizedName = string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : displayName.Trim().ToUpperInvariant();
        var normalizedSignal = string.IsNullOrWhiteSpace(endpointSignal)
            ? string.Empty
            : endpointSignal.Trim().ToUpperInvariant();

        foreach (var rule in rules)
        {
            if (!IsRuleMatch(
                    rule,
                    normalizedVendor,
                    normalizedProduct,
                    normalizedAddress,
                    normalizedName,
                    normalizedSignal))
            {
                continue;
            }

            if (!ProfilesById.TryGetValue(rule.ProfileId, out var profile))
            {
                continue;
            }

            var hint = string.IsNullOrWhiteSpace(rule.BrandHint)
                ? "override"
                : rule.BrandHint;
            selection = new ThirdPartyHandshakeSelection(
                profile,
                hint,
                $"override:{rule.MatchReason}");
            return true;
        }

        selection = default!;
        return false;
    }

    private static bool IsRuleMatch(
        ExternalOverrideRule rule,
        string normalizedVendor,
        string normalizedProduct,
        string normalizedAddress,
        string normalizedName,
        string normalizedSignal)
    {
        if (rule.VendorIds.Count > 0 &&
            !rule.VendorIds.Contains(normalizedVendor, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.ProductIds.Count > 0 &&
            !rule.ProductIds.Contains(normalizedProduct, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.Ouis.Count > 0)
        {
            var oui = string.IsNullOrWhiteSpace(normalizedAddress) || normalizedAddress.Length < 6
                ? string.Empty
                : normalizedAddress[..6];
            if (!rule.Ouis.Contains(oui, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (rule.NameContains.Count > 0 &&
            !rule.NameContains.Any(token =>
                normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                normalizedSignal.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static List<ExternalOverrideRule> LoadExternalOverrideRules()
    {
        lock (OverrideSync)
        {
            try
            {
                if (!File.Exists(OverrideFilePath))
                {
                    _cachedExternalRules = [];
                    _overrideLastWriteUtc = DateTime.MinValue;
                    return _cachedExternalRules;
                }

                var info = new FileInfo(OverrideFilePath);
                if (info.LastWriteTimeUtc == _overrideLastWriteUtc && _cachedExternalRules.Count > 0)
                {
                    return _cachedExternalRules;
                }

                var json = File.ReadAllText(OverrideFilePath);
                var payload = JsonSerializer.Deserialize<ExternalOverridePayload>(json, OverrideJsonOptions);
                _cachedExternalRules = NormalizeExternalRules(payload?.Rules);
                _overrideLastWriteUtc = info.LastWriteTimeUtc;
                return _cachedExternalRules;
            }
            catch
            {
                _cachedExternalRules = [];
                return _cachedExternalRules;
            }
        }
    }

    private static List<ExternalOverrideRule> NormalizeExternalRules(List<ExternalOverrideRulePayload>? rules)
    {
        if (rules is null || rules.Count == 0)
        {
            return [];
        }

        var normalized = new List<ExternalOverrideRule>(rules.Count);
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.ProfileId))
            {
                continue;
            }

            var profileId = rule.ProfileId.Trim();
            if (!ProfilesById.ContainsKey(profileId))
            {
                continue;
            }

            normalized.Add(new ExternalOverrideRule(
                ProfileId: profileId,
                BrandHint: string.IsNullOrWhiteSpace(rule.BrandHint) ? string.Empty : rule.BrandHint.Trim().ToLowerInvariant(),
                MatchReason: string.IsNullOrWhiteSpace(rule.MatchReason) ? profileId : rule.MatchReason.Trim(),
                VendorIds: NormalizeTokens(rule.VendorIds),
                ProductIds: NormalizeTokens(rule.ProductIds),
                NameContains: NormalizeTokens(rule.NameContains),
                Ouis: NormalizeTokens(rule.Ouis)));
        }

        return normalized;
    }

    private static List<string> NormalizeTokens(List<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string BrandHint, string Reason) ResolveBrandHint(
        string? displayName,
        string? endpointSignal,
        string? vendorId,
        string? productId,
        string? deviceAddress)
    {
        var name = string.IsNullOrWhiteSpace(displayName)
            ? string.Empty
            : displayName.Trim();
        var signal = string.IsNullOrWhiteSpace(endpointSignal)
            ? string.Empty
            : endpointSignal.Trim();
        var merged = $"{name} {signal}";
        if (merged.Contains("gulikit", StringComparison.OrdinalIgnoreCase))
        {
            return ("gulikit", "name:keyword_gulikit");
        }

        if (merged.Contains("flydigi", StringComparison.OrdinalIgnoreCase))
        {
            return ("flydigi", "name:keyword_flydigi");
        }

        if (merged.Contains("easysmx", StringComparison.OrdinalIgnoreCase) ||
            merged.Contains("easy smx", StringComparison.OrdinalIgnoreCase))
        {
            return ("easysmx", "name:keyword_easysmx");
        }

        if (merged.Contains("gamesir", StringComparison.OrdinalIgnoreCase) ||
            merged.Contains("game sir", StringComparison.OrdinalIgnoreCase))
        {
            return ("gamesir", "name:keyword_gamesir");
        }

        var normalizedVendor = string.IsNullOrWhiteSpace(vendorId)
            ? string.Empty
            : vendorId.Trim().ToUpperInvariant();
        var normalizedProduct = string.IsNullOrWhiteSpace(productId)
            ? string.Empty
            : productId.Trim().ToUpperInvariant();
        if (string.Equals(normalizedVendor, "20BC", StringComparison.Ordinal) ||
            normalizedProduct is "5500" or "5501" or "5502")
        {
            return ("gamesir", "vidpid:gamesir");
        }

        if (string.Equals(normalizedVendor, "045E", StringComparison.Ordinal))
        {
            return ("xbox", "vidpid:microsoft");
        }

        if (string.Equals(normalizedVendor, "24C6", StringComparison.Ordinal) ||
            string.Equals(normalizedProduct, "541A", StringComparison.Ordinal))
        {
            return ("flydigi", "vidpid:flydigi");
        }

        if (merged.Contains("xbox", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(normalizedVendor, "045E", StringComparison.Ordinal) ||
             signal.Contains("xinput", StringComparison.OrdinalIgnoreCase) ||
             signal.Contains("xusb", StringComparison.OrdinalIgnoreCase) ||
             signal.Contains("ig_", StringComparison.OrdinalIgnoreCase)))
        {
            return ("xbox", "name:xbox_with_signal");
        }

        var normalizedAddress = AddressNormalizer.NormalizeAddress(deviceAddress);
        if (!string.IsNullOrWhiteSpace(normalizedAddress) && normalizedAddress.Length >= 6)
        {
            var oui = normalizedAddress[..6];
            if (string.Equals(oui, "A05A5F", StringComparison.OrdinalIgnoreCase))
            {
                return ("easysmx", "address:oui_a05a5f");
            }
        }

        return (string.Empty, string.Empty);
    }

    private sealed record ExternalOverridePayload(List<ExternalOverrideRulePayload> Rules);

    private sealed record ExternalOverrideRulePayload(
        string ProfileId,
        string BrandHint,
        string MatchReason,
        List<string> VendorIds,
        List<string> ProductIds,
        List<string> NameContains,
        List<string> Ouis);

    private sealed record ExternalOverrideRule(
        string ProfileId,
        string BrandHint,
        string MatchReason,
        List<string> VendorIds,
        List<string> ProductIds,
        List<string> NameContains,
        List<string> Ouis);
}
