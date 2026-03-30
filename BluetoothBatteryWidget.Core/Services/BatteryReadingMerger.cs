using BluetoothBatteryWidget.Core.Models;

namespace BluetoothBatteryWidget.Core.Services;

public static class BatteryReadingMerger
{
    public static IReadOnlyList<PnpBatteryReading> MergeByAddress(
        params IReadOnlyList<PnpBatteryReading>[] sourcesInPriorityOrder)
    {
        var byAddress = new Dictionary<string, PnpBatteryReading>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourcesInPriorityOrder)
        {
            foreach (var candidate in source)
            {
                var normalizedAddress = AddressNormalizer.NormalizeAddress(candidate.Address);
                if (string.IsNullOrEmpty(normalizedAddress))
                {
                    continue;
                }

                var normalizedCandidate = candidate with { Address = normalizedAddress };
                if (!byAddress.TryGetValue(normalizedAddress, out var existing))
                {
                    byAddress[normalizedAddress] = normalizedCandidate;
                    continue;
                }

                if (normalizedCandidate.BatteryPercent is not null)
                {
                    byAddress[normalizedAddress] = normalizedCandidate;
                    continue;
                }

                if (existing.BatteryPercent is null &&
                    string.IsNullOrWhiteSpace(existing.DisplayName) &&
                    !string.IsNullOrWhiteSpace(normalizedCandidate.DisplayName))
                {
                    byAddress[normalizedAddress] = normalizedCandidate;
                }
            }
        }

        return byAddress.Values.ToList();
    }
}
