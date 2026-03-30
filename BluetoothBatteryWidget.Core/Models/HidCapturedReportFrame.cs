namespace BluetoothBatteryWidget.Core.Models;

public sealed record HidCapturedReportFrame(
    byte ReportId,
    byte[] Data,
    DateTimeOffset CapturedAt
);
