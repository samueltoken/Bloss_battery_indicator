using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.App.Services;

internal static class DualSensePairingInfoParser
{
    internal const byte ReportId = 0x09;
    internal const int ReportSize = 20;
    private const int AddressByteCount = 6;

    public static bool TryParseControllerAddress(ReadOnlySpan<byte> report, out string address)
    {
        address = string.Empty;
        if (report.Length < ReportSize || report[0] != ReportId)
        {
            return false;
        }

        var rawAddress = report.Slice(1, AddressByteCount);
        if (IsFilledWith(rawAddress, 0x00) || IsFilledWith(rawAddress, 0xFF))
        {
            return false;
        }

        Span<byte> displayOrder = stackalloc byte[AddressByteCount];
        for (var index = 0; index < AddressByteCount; index++)
        {
            displayOrder[index] = rawAddress[AddressByteCount - 1 - index];
        }

        var normalized = AddressNormalizer.NormalizeAddress(Convert.ToHexString(displayOrder));
        if (normalized.Length != AddressByteCount * 2)
        {
            return false;
        }

        address = normalized;
        return true;
    }

    private static bool IsFilledWith(ReadOnlySpan<byte> value, byte expected)
    {
        foreach (var item in value)
        {
            if (item != expected)
            {
                return false;
            }
        }

        return true;
    }
}
