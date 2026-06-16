using System.Globalization;

namespace BluetoothBatteryWidget.App.Services;

internal sealed record BatteryGuideTrigger(
    GuideButtonDeviceKind DeviceKind,
    byte ReportId,
    IReadOnlyList<BatteryGuideTriggerBit> Bits,
    string DisplayName)
{
    public string ToPersistedString()
    {
        var bits = string.Join(
            ',',
            Bits
                .OrderBy(bit => bit.Offset)
                .ThenBy(bit => bit.Mask)
                .Select(bit => $"{bit.Offset:X2}:{bit.Mask:X2}"));

        return $"{DeviceKind}|{ReportId:X2}|{bits}|{DisplayName.Replace("|", " ").Trim()}";
    }
}

internal sealed record BatteryGuideTriggerBit(int Offset, byte Mask);

internal static class BatteryGuideTriggerParser
{
    private const int AxisPseudoOffsetBase = 0x80;
    private const int DualSenseDPadPseudoOffsetBase = 0x90;
    private const byte AxisNegativeMask = 0x01;
    private const byte AxisPositiveMask = 0x02;
    private const short AxisActivationThreshold = 6000;

    private static readonly IReadOnlyDictionary<(byte ReportId, int Offset, byte Mask), (string DisplayName, string VisualKey)> SteamTritonButtons =
        new Dictionary<(byte ReportId, int Offset, byte Mask), (string DisplayName, string VisualKey)>
        {
            [(0x42, 2, 0x01)] = ("A", "A"),
            [(0x42, 2, 0x02)] = ("B", "B"),
            [(0x42, 2, 0x04)] = ("X", "X"),
            [(0x42, 2, 0x08)] = ("Y", "Y"),
            [(0x42, 2, 0x10)] = ("Quick Access", "QuickAccess"),
            [(0x42, 2, 0x20)] = ("R3", "RightPad"),
            [(0x42, 2, 0x40)] = ("Menu", "Menu"),
            [(0x42, 2, 0x80)] = ("R4", "R4"),
            [(0x42, 3, 0x01)] = ("R5", "R5"),
            [(0x42, 3, 0x02)] = ("RB", "RB"),
            [(0x42, 3, 0x04)] = ("Down", "Down"),
            [(0x42, 3, 0x08)] = ("Right", "Right"),
            [(0x42, 3, 0x10)] = ("Left", "Left"),
            [(0x42, 3, 0x20)] = ("Up", "Up"),
            [(0x42, 3, 0x40)] = ("View", "View"),
            [(0x42, 3, 0x80)] = ("L3", "LeftPad"),
            [(0x42, 4, 0x01)] = ("Guide", "Guide"),
            [(0x42, 4, 0x02)] = ("L4", "L4"),
            [(0x42, 4, 0x04)] = ("L5", "L5"),
            [(0x42, 4, 0x08)] = ("LB", "LB"),
            [(0x42, 4, 0x40)] = ("Right Pad", "RightPad"),
            [(0x42, 4, 0x80)] = ("RT", "RT"),
            [(0x42, 5, 0x04)] = ("Left Pad", "LeftPad"),
            [(0x42, 5, 0x08)] = ("LT", "LT"),
            [(0x45, 2, 0x01)] = ("A", "A"),
            [(0x45, 2, 0x02)] = ("B", "B"),
            [(0x45, 2, 0x04)] = ("X", "X"),
            [(0x45, 2, 0x08)] = ("Y", "Y"),
            [(0x45, 2, 0x10)] = ("Quick Access", "QuickAccess"),
            [(0x45, 2, 0x20)] = ("R3", "RightPad"),
            [(0x45, 2, 0x40)] = ("Menu", "Menu"),
            [(0x45, 2, 0x80)] = ("R4", "R4"),
            [(0x45, 3, 0x01)] = ("R5", "R5"),
            [(0x45, 3, 0x02)] = ("RB", "RB"),
            [(0x45, 3, 0x04)] = ("Down", "Down"),
            [(0x45, 3, 0x08)] = ("Right", "Right"),
            [(0x45, 3, 0x10)] = ("Left", "Left"),
            [(0x45, 3, 0x20)] = ("Up", "Up"),
            [(0x45, 3, 0x40)] = ("View", "View"),
            [(0x45, 3, 0x80)] = ("L3", "LeftPad"),
            [(0x45, 4, 0x01)] = ("Guide", "Guide"),
            [(0x45, 4, 0x02)] = ("L4", "L4"),
            [(0x45, 4, 0x04)] = ("L5", "L5"),
            [(0x45, 4, 0x08)] = ("LB", "LB"),
            [(0x45, 4, 0x40)] = ("Right Pad", "RightPad"),
            [(0x45, 4, 0x80)] = ("RT", "RT"),
            [(0x45, 5, 0x04)] = ("Left Pad", "LeftPad"),
            [(0x45, 5, 0x08)] = ("LT", "LT")
        };

    private static readonly IReadOnlyDictionary<(byte ReportId, int Offset, byte Mask), (string DisplayName, string VisualKey)> DualSenseButtons =
        new Dictionary<(byte ReportId, int Offset, byte Mask), (string DisplayName, string VisualKey)>
        {
            [(0x01, 7, 0x01)] = ("PS", "Guide"),
            [(0x01, 8, 0x10)] = ("Square", "X"),
            [(0x01, 8, 0x20)] = ("Cross", "A"),
            [(0x01, 8, 0x40)] = ("Circle", "B"),
            [(0x01, 8, 0x80)] = ("Triangle", "Y"),
            [(0x01, 9, 0x01)] = ("L1", "LB"),
            [(0x01, 9, 0x02)] = ("R1", "RB"),
            [(0x01, 9, 0x04)] = ("L2", "LT"),
            [(0x01, 9, 0x08)] = ("R2", "RT"),
            [(0x01, 9, 0x10)] = ("Create", "View"),
            [(0x01, 9, 0x20)] = ("Options", "Menu"),
            [(0x01, 9, 0x40)] = ("L3", "LeftPad"),
            [(0x01, 9, 0x80)] = ("R3", "RightPad"),
            [(0x01, 10, 0x01)] = ("PS", "Guide"),
            [(0x01, 10, 0x02)] = ("Touch Pad", string.Empty),
            [(0x01, 10, 0x04)] = ("Mic", "Mic"),
            [(0x31, 9, 0x10)] = ("Square", "X"),
            [(0x31, 9, 0x20)] = ("Cross", "A"),
            [(0x31, 9, 0x40)] = ("Circle", "B"),
            [(0x31, 9, 0x80)] = ("Triangle", "Y"),
            [(0x31, 10, 0x01)] = ("L1", "LB"),
            [(0x31, 10, 0x02)] = ("R1", "RB"),
            [(0x31, 10, 0x04)] = ("L2", "LT"),
            [(0x31, 10, 0x08)] = ("R2", "RT"),
            [(0x31, 10, 0x10)] = ("Create", "View"),
            [(0x31, 10, 0x20)] = ("Options", "Menu"),
            [(0x31, 10, 0x40)] = ("L3", "LeftPad"),
            [(0x31, 10, 0x80)] = ("R3", "RightPad"),
            [(0x31, 11, 0x01)] = ("PS", "Guide"),
            [(0x31, 11, 0x02)] = ("Touch Pad", string.Empty),
            [(0x31, 11, 0x04)] = ("Mic", "Mic")
        };

    public static bool TryParse(string? value, out BatteryGuideTrigger trigger)
    {
        trigger = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|', 4, StringSplitOptions.TrimEntries);
        if (parts.Length < 3 ||
            !Enum.TryParse(parts[0], ignoreCase: true, out GuideButtonDeviceKind deviceKind) ||
            !byte.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var reportId))
        {
            return false;
        }

        var bits = new List<BatteryGuideTriggerBit>();
        foreach (var bitPart in parts[2].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var bitFields = bitPart.Split(':', 2, StringSplitOptions.TrimEntries);
            if (bitFields.Length != 2 ||
                !int.TryParse(bitFields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset) ||
                !byte.TryParse(bitFields[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var mask) ||
                offset < 0 ||
                mask == 0)
            {
                return false;
            }

            bits.Add(new BatteryGuideTriggerBit(offset, mask));
        }

        if (bits.Count == 0)
        {
            return false;
        }

        var displayName = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3])
            ? parts[3].Trim()
            : "Custom button";
        trigger = new BatteryGuideTrigger(deviceKind, reportId, bits, displayName);
        return true;
    }

    public static bool TryCapture(
        GuideButtonDeviceKind deviceKind,
        ReadOnlySpan<byte> previousReport,
        ReadOnlySpan<byte> currentReport,
        out BatteryGuideTrigger trigger)
    {
        trigger = null!;
        if (currentReport.Length == 0 || previousReport.Length == 0)
        {
            return false;
        }

        var reportId = currentReport[0];
        if (previousReport[0] != reportId)
        {
            return false;
        }

        var offsets = deviceKind == GuideButtonDeviceKind.DualSense &&
            LooksLikePaddedShortBluetoothDualSenseReport(currentReport)
                ? [7]
                : GetButtonOffsets(deviceKind, reportId, currentReport.Length);
        var bits = new List<BatteryGuideTriggerBit>();
        var steamDPadFallbackBits = new List<BatteryGuideTriggerBit>();
        var hasNewPress = false;
        foreach (var offset in offsets)
        {
            if (offset >= currentReport.Length || offset >= previousReport.Length)
            {
                continue;
            }

            var pressed = currentReport[offset];
            for (var bit = 0; bit < 8; bit++)
            {
                var mask = (byte)(1 << bit);
                if ((pressed & mask) == 0 ||
                    !ShouldCaptureButtonBit(deviceKind, reportId, offset, mask))
                {
                    continue;
                }

                if (IsSteamTritonDPadBit(deviceKind, reportId, offset, mask))
                {
                    steamDPadFallbackBits.Add(new BatteryGuideTriggerBit(offset, mask));
                    continue;
                }

                if ((previousReport[offset] & mask) == 0)
                {
                    hasNewPress = true;
                }

                bits.Add(new BatteryGuideTriggerBit(offset, mask));
            }
        }

        if (deviceKind == GuideButtonDeviceKind.SteamController &&
            reportId is 0x42 or 0x45)
        {
            foreach (var axisBit in CaptureSteamControllerAxes(previousReport, currentReport, out var axisHasNewPress))
            {
                hasNewPress |= axisHasNewPress;
                bits.Add(axisBit);
            }

            if (!bits.Any(IsSteamLeftPadAxisPseudoBit) &&
                steamDPadFallbackBits.Count == 1)
            {
                var dPadBit = steamDPadFallbackBits[0];
                if ((previousReport[dPadBit.Offset] & dPadBit.Mask) == 0)
                {
                    hasNewPress = true;
                }

                bits.Add(dPadBit);
            }
        }
        else if (deviceKind == GuideButtonDeviceKind.DualSense &&
            TryGetDualSenseDPadPseudoBit(previousReport, currentReport, out var dPadBit, out var dPadHasNewPress))
        {
            hasNewPress |= dPadHasNewPress;
            bits.Add(dPadBit);
        }

        if (!hasNewPress || bits.Count == 0)
        {
            return false;
        }

        trigger = new BatteryGuideTrigger(
            deviceKind,
            reportId,
            bits,
            BuildDisplayName(deviceKind, reportId, bits));
        return true;
    }

    public static byte[] CreateNeutralReportForCapture(
        GuideButtonDeviceKind deviceKind,
        ReadOnlySpan<byte> currentReport)
    {
        if (currentReport.Length == 0)
        {
            return [];
        }

        if (deviceKind != GuideButtonDeviceKind.DualSense)
        {
            return currentReport.ToArray();
        }

        var neutralReport = new byte[currentReport.Length];
        neutralReport[0] = currentReport[0];
        if (TryGetDualSenseDPadOffset(currentReport[0], currentReport.Length, out var dPadOffset))
        {
            neutralReport[dPadOffset] = 0x08;
        }

        return neutralReport;
    }

    public static bool IsMatch(BatteryGuideTrigger trigger, GuideButtonDeviceKind deviceKind, ReadOnlySpan<byte> report)
    {
        if (trigger.DeviceKind != deviceKind ||
            report.Length == 0 ||
            report[0] != trigger.ReportId)
        {
            return false;
        }

        foreach (var bit in trigger.Bits)
        {
            if (IsAxisPseudoBit(bit))
            {
                if (!IsAxisPseudoBitPressed(trigger.DeviceKind, trigger.ReportId, bit, report))
                {
                    return false;
                }

                continue;
            }

            if (IsDualSenseDPadPseudoBit(bit))
            {
                if (!IsDualSenseDPadPseudoBitPressed(trigger.DeviceKind, trigger.ReportId, bit, report))
                {
                    return false;
                }

                continue;
            }

            if (bit.Offset < 0 ||
                bit.Offset >= report.Length ||
                (report[bit.Offset] & bit.Mask) != bit.Mask)
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasAnyTriggerBitPressed(
        BatteryGuideTrigger trigger,
        GuideButtonDeviceKind deviceKind,
        ReadOnlySpan<byte> report)
    {
        if (trigger.DeviceKind != deviceKind ||
            report.Length == 0 ||
            report[0] != trigger.ReportId)
        {
            return false;
        }

        foreach (var bit in trigger.Bits)
        {
            if (IsAxisPseudoBit(bit))
            {
                if (IsAxisPseudoBitPressed(trigger.DeviceKind, trigger.ReportId, bit, report))
                {
                    return true;
                }

                continue;
            }

            if (IsDualSenseDPadPseudoBit(bit))
            {
                if (IsDualSenseDPadPseudoBitPressed(trigger.DeviceKind, trigger.ReportId, bit, report))
                {
                    return true;
                }

                continue;
            }

            if (bit.Offset >= 0 &&
                bit.Offset < report.Length &&
                (report[bit.Offset] & bit.Mask) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> GetVisualButtonKeys(BatteryGuideTrigger trigger)
    {
        return trigger.Bits
            .Select(bit => GetButtonInfo(trigger.DeviceKind, trigger.ReportId, bit).VisualKey)
            .SelectMany(key => key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDisplayName(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        IReadOnlyList<BatteryGuideTriggerBit> bits)
    {
        var names = bits
            .OrderBy(bit => GetDisplaySortOrder(deviceKind, reportId, bit))
            .ThenBy(bit => bit.Mask)
            .Select(bit => GetButtonInfo(deviceKind, reportId, bit).DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return names.Length == 0
            ? (bits.Count == 1 ? "Custom button" : "Custom combo")
            : string.Join(" + ", names);
    }

    private static int GetDisplaySortOrder(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        BatteryGuideTriggerBit bit)
    {
        var info = GetButtonInfo(deviceKind, reportId, bit);
        if (deviceKind == GuideButtonDeviceKind.DualSense &&
            string.Equals(info.VisualKey, "Guide", StringComparison.OrdinalIgnoreCase))
        {
            return -1;
        }

        return (bit.Offset * 0x100) + bit.Mask;
    }

    private static (string DisplayName, string VisualKey) GetButtonInfo(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        BatteryGuideTriggerBit bit)
    {
        if (deviceKind == GuideButtonDeviceKind.SteamController)
        {
            if (IsAxisPseudoBit(bit))
            {
                return GetAxisPseudoButtonInfo(bit);
            }

            if (SteamTritonButtons.TryGetValue((reportId, bit.Offset, bit.Mask), out var info))
            {
                return info;
            }

            if (bit.Offset == 9 && bit.Mask == 0x20)
            {
                return ("Guide", "Guide");
            }
        }

        if (deviceKind == GuideButtonDeviceKind.DualSense)
        {
            if (IsDualSenseDPadPseudoBit(bit))
            {
                return GetDualSenseDPadButtonInfo(bit);
            }

            if (DualSenseButtons.TryGetValue((reportId, bit.Offset, bit.Mask), out var info))
            {
                return info;
            }
        }

        return ($"Button {bit.Offset:X2}:{bit.Mask:X2}", string.Empty);
    }

    private static IReadOnlyList<int> GetButtonOffsets(GuideButtonDeviceKind deviceKind, byte reportId, int reportLength)
    {
        return deviceKind switch
        {
            GuideButtonDeviceKind.DualSense when reportId == 0x01 && reportLength >= 11 => [8, 9, 10],
            GuideButtonDeviceKind.DualSense when reportId == 0x31 && reportLength >= 12 => [9, 10, 11],
            GuideButtonDeviceKind.SteamController when reportId is 0x42 or 0x45 => [2, 3, 4, 5],
            GuideButtonDeviceKind.SteamController => [9],
            _ => []
        };
    }

    private static bool ShouldCaptureButtonBit(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        int offset,
        byte mask)
    {
        if (deviceKind == GuideButtonDeviceKind.DualSense)
        {
            return DualSenseButtons.ContainsKey((reportId, offset, mask));
        }

        return deviceKind != GuideButtonDeviceKind.SteamController ||
            SteamTritonButtons.ContainsKey((reportId, offset, mask)) ||
            (offset == 9 && mask == 0x20);
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

    private static bool IsSteamTritonDPadBit(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        int offset,
        byte mask)
    {
        return deviceKind == GuideButtonDeviceKind.SteamController &&
               reportId is 0x42 or 0x45 &&
               offset == 3 &&
               mask is 0x04 or 0x08 or 0x10 or 0x20;
    }

    private static bool IsSteamLeftPadAxisPseudoBit(BatteryGuideTriggerBit bit)
    {
        if (!IsAxisPseudoBit(bit))
        {
            return false;
        }

        var axis = (SteamAxisKind)(bit.Offset - AxisPseudoOffsetBase);
        return axis is SteamAxisKind.LeftPadX or SteamAxisKind.LeftPadY;
    }

    private static IReadOnlyList<BatteryGuideTriggerBit> CaptureSteamControllerAxes(
        ReadOnlySpan<byte> previousReport,
        ReadOnlySpan<byte> currentReport,
        out bool hasNewPress)
    {
        hasNewPress = false;
        var capturedNewPress = false;
        var bits = new List<BatteryGuideTriggerBit>();

        AddDominantAxisDirection(bits, previousReport, currentReport, SteamAxisKind.LeftStickX, SteamAxisKind.LeftStickY);
        AddDominantAxisDirection(bits, previousReport, currentReport, SteamAxisKind.RightStickX, SteamAxisKind.RightStickY);
        AddDominantAxisDirection(bits, previousReport, currentReport, SteamAxisKind.LeftPadX, SteamAxisKind.LeftPadY);
        AddDominantAxisDirection(bits, previousReport, currentReport, SteamAxisKind.RightPadX, SteamAxisKind.RightPadY);

        hasNewPress = capturedNewPress;
        return bits;

        void AddDominantAxisDirection(
            List<BatteryGuideTriggerBit> target,
            ReadOnlySpan<byte> previous,
            ReadOnlySpan<byte> current,
            SteamAxisKind xAxis,
            SteamAxisKind yAxis)
        {
            if (!TryGetDominantAxisBit(current, xAxis, yAxis, out var bit))
            {
                return;
            }

            if (!IsAxisPseudoBitPressed(GuideButtonDeviceKind.SteamController, current[0], bit, previous))
            {
                capturedNewPress = true;
            }

            target.Add(bit);
        }
    }

    private static bool TryGetDominantAxisBit(
        ReadOnlySpan<byte> report,
        SteamAxisKind xAxis,
        SteamAxisKind yAxis,
        out BatteryGuideTriggerBit bit)
    {
        bit = null!;
        if (!TryReadSteamAxis(report, xAxis, out var xValue) ||
            !TryReadSteamAxis(report, yAxis, out var yValue))
        {
            return false;
        }

        var useX = Math.Abs(xValue) >= Math.Abs(yValue);
        var axis = useX ? xAxis : yAxis;
        var value = useX ? xValue : yValue;
        if (Math.Abs(value) < AxisActivationThreshold)
        {
            return false;
        }

        bit = new BatteryGuideTriggerBit(
            AxisPseudoOffsetBase + (int)axis,
            value < 0 ? AxisNegativeMask : AxisPositiveMask);
        return true;
    }

    private static bool IsAxisPseudoBit(BatteryGuideTriggerBit bit)
    {
        return bit.Offset >= AxisPseudoOffsetBase &&
               Enum.IsDefined(typeof(SteamAxisKind), bit.Offset - AxisPseudoOffsetBase) &&
               bit.Mask is AxisNegativeMask or AxisPositiveMask;
    }

    private static bool IsDualSenseDPadPseudoBit(BatteryGuideTriggerBit bit)
    {
        return bit.Offset >= DualSenseDPadPseudoOffsetBase &&
               bit.Offset < DualSenseDPadPseudoOffsetBase + 0x20 &&
               bit.Mask is >= 1 and <= 8;
    }

    private static bool TryGetDualSenseDPadPseudoBit(
        ReadOnlySpan<byte> previousReport,
        ReadOnlySpan<byte> currentReport,
        out BatteryGuideTriggerBit bit,
        out bool hasNewPress)
    {
        bit = null!;
        hasNewPress = false;
        if (!TryGetDualSenseDPadOffset(currentReport[0], currentReport.Length, out var offset) ||
            offset >= previousReport.Length)
        {
            return false;
        }

        var currentDirection = currentReport[offset] & 0x0F;
        if (currentDirection > 7)
        {
            return false;
        }

        var previousDirection = previousReport[offset] & 0x0F;
        hasNewPress = previousDirection > 7 || previousDirection != currentDirection;
        bit = new BatteryGuideTriggerBit(
            DualSenseDPadPseudoOffsetBase + offset,
            (byte)(currentDirection + 1));
        return true;
    }

    private static bool TryGetDualSenseDPadOffset(byte reportId, int reportLength, out int offset)
    {
        offset = reportId switch
        {
            0x01 when reportLength >= 9 => 8,
            0x31 when reportLength >= 10 => 9,
            _ => -1
        };

        return offset >= 0;
    }

    private static bool IsDualSenseDPadPseudoBitPressed(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        BatteryGuideTriggerBit bit,
        ReadOnlySpan<byte> report)
    {
        if (deviceKind != GuideButtonDeviceKind.DualSense ||
            !IsDualSenseDPadPseudoBit(bit))
        {
            return false;
        }

        var offset = bit.Offset - DualSenseDPadPseudoOffsetBase;
        if (!TryGetDualSenseDPadOffset(reportId, report.Length, out var expectedOffset) ||
            offset != expectedOffset)
        {
            return false;
        }

        var expectedDirection = bit.Mask - 1;
        return (report[offset] & 0x0F) == expectedDirection;
    }

    private static (string DisplayName, string VisualKey) GetDualSenseDPadButtonInfo(BatteryGuideTriggerBit bit)
    {
        return (bit.Mask - 1) switch
        {
            0 => ("D-Pad Up", "Up"),
            1 => ("D-Pad Up Right", "Up|Right"),
            2 => ("D-Pad Right", "Right"),
            3 => ("D-Pad Down Right", "Down|Right"),
            4 => ("D-Pad Down", "Down"),
            5 => ("D-Pad Down Left", "Down|Left"),
            6 => ("D-Pad Left", "Left"),
            7 => ("D-Pad Up Left", "Up|Left"),
            _ => ("D-Pad", string.Empty)
        };
    }

    private static bool IsAxisPseudoBitPressed(
        GuideButtonDeviceKind deviceKind,
        byte reportId,
        BatteryGuideTriggerBit bit,
        ReadOnlySpan<byte> report)
    {
        if (deviceKind != GuideButtonDeviceKind.SteamController ||
            reportId is not (0x42 or 0x45) ||
            !IsAxisPseudoBit(bit))
        {
            return false;
        }

        var axis = (SteamAxisKind)(bit.Offset - AxisPseudoOffsetBase);
        if (!TryReadSteamAxis(report, axis, out var value) ||
            Math.Abs(value) < AxisActivationThreshold)
        {
            return false;
        }

        return bit.Mask == AxisNegativeMask
            ? value < 0
            : value > 0;
    }

    private static (string DisplayName, string VisualKey) GetAxisPseudoButtonInfo(BatteryGuideTriggerBit bit)
    {
        var axis = (SteamAxisKind)(bit.Offset - AxisPseudoOffsetBase);
        var negative = bit.Mask == AxisNegativeMask;
        return axis switch
        {
            SteamAxisKind.LeftStickX => negative
                ? ("Left Stick Left", "LeftPad")
                : ("Left Stick Right", "LeftPad"),
            SteamAxisKind.LeftStickY => negative
                ? ("Left Stick Up", "LeftPad")
                : ("Left Stick Down", "LeftPad"),
            SteamAxisKind.RightStickX => negative
                ? ("Right Stick Left", "RightPad")
                : ("Right Stick Right", "RightPad"),
            SteamAxisKind.RightStickY => negative
                ? ("Right Stick Up", "RightPad")
                : ("Right Stick Down", "RightPad"),
            SteamAxisKind.LeftPadX => negative
                ? ("Left Pad Left", "Left")
                : ("Left Pad Right", "Right"),
            SteamAxisKind.LeftPadY => negative
                ? ("Left Pad Up", "Up")
                : ("Left Pad Down", "Down"),
            SteamAxisKind.RightPadX => negative
                ? ("Right Pad Left", "RightPad")
                : ("Right Pad Right", "RightPad"),
            SteamAxisKind.RightPadY => negative
                ? ("Right Pad Up", "RightPad")
                : ("Right Pad Down", "RightPad"),
            _ => ("Custom axis", string.Empty)
        };
    }

    private static bool TryReadSteamAxis(ReadOnlySpan<byte> report, SteamAxisKind axis, out short value)
    {
        value = 0;
        var offset = axis switch
        {
            SteamAxisKind.LeftStickX => 10,
            SteamAxisKind.LeftStickY => 12,
            SteamAxisKind.RightStickX => 14,
            SteamAxisKind.RightStickY => 16,
            SteamAxisKind.LeftPadX => 18,
            SteamAxisKind.LeftPadY => 20,
            SteamAxisKind.RightPadX => 24,
            SteamAxisKind.RightPadY => 26,
            _ => -1
        };

        if (offset < 0 || report.Length <= offset + 1)
        {
            return false;
        }

        value = (short)(report[offset] | (report[offset + 1] << 8));
        return true;
    }

    private enum SteamAxisKind
    {
        LeftStickX = 0,
        LeftStickY = 1,
        RightStickX = 2,
        RightStickY = 3,
        LeftPadX = 4,
        LeftPadY = 5,
        RightPadX = 6,
        RightPadY = 7
    }
}
