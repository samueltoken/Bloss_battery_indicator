namespace BluetoothBatteryWidget.App.Services;

internal enum GuideButtonDeviceKind
{
    DualSense,
    SteamController
}

internal static class GuideButtonReportParser
{
    public static bool TryParseGuideButton(
        GuideButtonDeviceKind deviceKind,
        ReadOnlySpan<byte> report,
        out bool isPressed)
    {
        isPressed = false;
        return deviceKind switch
        {
            GuideButtonDeviceKind.DualSense => TryParseDualSenseGuideButton(report, out isPressed),
            GuideButtonDeviceKind.SteamController => TryParseSteamControllerGuideButton(report, out isPressed),
            _ => false
        };
    }

    private static bool TryParseDualSenseGuideButton(ReadOnlySpan<byte> report, out bool isPressed)
    {
        isPressed = false;
        if (report.Length == 0)
        {
            return false;
        }

        switch (report[0])
        {
            case 0x01:
                if (LooksLikePaddedShortBluetoothDualSenseReport(report))
                {
                    isPressed = (report[7] & 0x01) != 0;
                    return true;
                }

                if (report.Length >= 11)
                {
                    isPressed = (report[10] & 0x01) != 0;
                    return true;
                }

                if (report.Length >= 8)
                {
                    isPressed = (report[7] & 0x01) != 0;
                    return true;
                }

                return false;

            case 0x31 when report.Length >= 12:
                isPressed = (report[11] & 0x01) != 0;
                return true;

            default:
                return false;
        }
    }

    private static bool LooksLikePaddedShortBluetoothDualSenseReport(ReadOnlySpan<byte> report)
    {
        if (report.Length < 11 || report[0] != 0x01)
        {
            return false;
        }

        var scanLength = Math.Min(report.Length, 64);
        for (var index = 10; index < scanLength; index++)
        {
            if (report[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseSteamControllerGuideButton(ReadOnlySpan<byte> report, out bool isPressed)
    {
        isPressed = false;
        if (report.Length < 10)
        {
            return false;
        }

        if (TryParseSteamControllerTritonStateReport(report, out isPressed))
        {
            return true;
        }

        if (TryParseSteamControllerEnvelope(report, out isPressed))
        {
            return true;
        }

        if (report[0] is 0x43 or 0x44 or 0x46 or 0x79 or 0x7B)
        {
            return false;
        }

        isPressed = (report[9] & 0x20) != 0;
        return true;
    }

    private static bool TryParseSteamControllerTritonStateReport(ReadOnlySpan<byte> report, out bool isPressed)
    {
        isPressed = false;
        if (report.Length < 5)
        {
            return false;
        }

        if (report[0] == 0x42)
        {
            isPressed = (report[4] & 0x01) != 0;
            return true;
        }

        if (report[0] == 0x45)
        {
            isPressed = (report[4] & 0x01) != 0;
            return true;
        }

        return false;
    }

    private static bool TryParseSteamControllerEnvelope(ReadOnlySpan<byte> report, out bool isPressed)
    {
        isPressed = false;
        if (TryParseSteamControllerEnvelopeAt(report, 0, out isPressed))
        {
            return true;
        }

        return report.Length >= 11 &&
               report[0] == 0x00 &&
               TryParseSteamControllerEnvelopeAt(report, 1, out isPressed);
    }

    private static bool TryParseSteamControllerEnvelopeAt(ReadOnlySpan<byte> report, int offset, out bool isPressed)
    {
        isPressed = false;
        if (report.Length < offset + 10 ||
            report[offset] != 0x01 ||
            report[offset + 1] != 0x00)
        {
            return false;
        }

        if (report[offset + 2] != 0x01)
        {
            return true;
        }

        isPressed = (report[offset + 9] & 0x20) != 0;
        return true;
    }
}
