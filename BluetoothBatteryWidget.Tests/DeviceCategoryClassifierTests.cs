using BluetoothBatteryWidget.Core.Models;
using BluetoothBatteryWidget.Core.Services;

namespace BluetoothBatteryWidget.Tests;

public sealed class DeviceCategoryClassifierTests
{
    [Theory]
    [InlineData("GameSir G7 SE")]
    [InlineData("GuliKit Controller XW")]
    [InlineData("Flydigi Vader 4 Pro")]
    [InlineData("EasySMX D10")]
    [InlineData("Xbox Wireless Controller")]
    public void Classify_ThirdPartyControllerKeywords_ReturnsGamepad(string name)
    {
        var category = DeviceCategoryClassifier.Classify(name, categoryHint: null);

        Assert.Equal(DeviceCategory.Gamepad, category);
    }

    [Fact]
    public void Classify_KoreanGamepadKeyword_ReturnsGamepad()
    {
        var category = DeviceCategoryClassifier.Classify("무선 게임패드", categoryHint: null);

        Assert.Equal(DeviceCategory.Gamepad, category);
    }
}
