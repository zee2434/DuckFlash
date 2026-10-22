using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji parser output mentah dari adb dan fastboot.
/// </summary>
public class DeviceOutputParserTests
{
    [Fact]
    public void ParseAdbDevices_ReadsSerialAndStatus()
    {
        const string output = "List of devices attached\nABC123\tdevice\nDEF456\tunauthorized\n";

        var devices = DeviceOutputParser.ParseAdbDevices(output);

        Assert.Equal(2, devices.Count);
        Assert.Equal("ABC123", devices[0].Serial);
        Assert.Equal("device", devices[0].Status);
        Assert.True(devices[0].IsAdb);
        Assert.Equal("DEF456", devices[1].Serial);
        Assert.Equal("unauthorized", devices[1].Status);
    }

    [Fact]
    public void ParseAdbDevices_SkipsHeaderAndDaemonLines()
    {
        const string output = "* daemon not running\n* daemon started successfully\nList of devices attached\nABC123\tdevice\n";

        var devices = DeviceOutputParser.ParseAdbDevices(output);

        Assert.Single(devices);
        Assert.Equal("ABC123", devices[0].Serial);
    }

    [Fact]
    public void ParseAdbDevices_EmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(DeviceOutputParser.ParseAdbDevices(string.Empty));
        Assert.Empty(DeviceOutputParser.ParseAdbDevices(null));
        Assert.Empty(DeviceOutputParser.ParseAdbDevices("List of devices attached\n"));
    }

    [Fact]
    public void ParseFastbootDevices_ReadsSerialAndStatus()
    {
        const string output = "ABC123\tfastboot\nDEF456\tfastboot\n";

        var devices = DeviceOutputParser.ParseFastbootDevices(output);

        Assert.Equal(2, devices.Count);
        Assert.Equal("ABC123", devices[0].Serial);
        Assert.Equal("fastboot", devices[0].Status);
        Assert.True(devices[0].IsFastboot);
    }

    [Fact]
    public void ParseFastbootDevices_EmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(DeviceOutputParser.ParseFastbootDevices(string.Empty));
        Assert.Empty(DeviceOutputParser.ParseFastbootDevices(null));
    }

    [Fact]
    public void ParseGetProp_ReadsKeysAndValues()
    {
        const string output =
            "[ro.product.model]: [Pixel 7]\n" +
            "[ro.product.device]: [panther]\n" +
            "[ro.build.version.release]: [14]\n";

        var properties = DeviceOutputParser.ParseGetProp(output);

        Assert.Equal("Pixel 7", properties["ro.product.model"]);
        Assert.Equal("panther", properties["ro.product.device"]);
        Assert.Equal("14", properties["ro.build.version.release"]);
    }

    [Fact]
    public void ParseGetProp_IgnoresMalformedLines()
    {
        const string output = "garbage tanpa kurung\n[ro.product.model]: [Pixel 7]\n";

        var properties = DeviceOutputParser.ParseGetProp(output);

        Assert.Single(properties);
        Assert.Equal("Pixel 7", properties["ro.product.model"]);
    }

    [Fact]
    public void ParseGetProp_EmptyOutput_ReturnsEmptyDictionary()
    {
        Assert.Empty(DeviceOutputParser.ParseGetProp(string.Empty));
        Assert.Empty(DeviceOutputParser.ParseGetProp(null));
    }

    [Fact]
    public void ParseGetVar_ReadsKeysAndValues()
    {
        const string output =
            "product: panther\n" +
            "unlocked: yes\n" +
            "current-slot: a\n" +
            "secure: yes\n";

        var variables = DeviceOutputParser.ParseGetVar(output);

        Assert.Equal("panther", variables["product"]);
        Assert.Equal("yes", variables["unlocked"]);
        Assert.Equal("a", variables["current-slot"]);
    }

    [Fact]
    public void ParseGetVar_IgnoresLinesWithoutSeparator()
    {
        const string output = "garisanpaformat\nproduct: panther\n";

        var variables = DeviceOutputParser.ParseGetVar(output);

        Assert.Single(variables);
        Assert.Equal("panther", variables["product"]);
    }

    [Fact]
    public void ParseGetVar_EmptyOutput_ReturnsEmptyDictionary()
    {
        Assert.Empty(DeviceOutputParser.ParseGetVar(string.Empty));
        Assert.Empty(DeviceOutputParser.ParseGetVar(null));
    }

    [Theory]
    [InlineData("yes", "unlocked")]
    [InlineData("YES", "unlocked")]
    [InlineData("no", "locked")]
    [InlineData("No", "locked")]
    public void ParseBootloaderState_ReadsUnlockedFlag(string value, string expected)
    {
        var variables = new Dictionary<string, string> { ["unlocked"] = value };

        Assert.Equal(expected, DeviceOutputParser.ParseBootloaderState(variables));
    }

    [Fact]
    public void ParseBootloaderState_MissingKey_ReturnsUnknown()
    {
        Assert.Equal("tidak diketahui", DeviceOutputParser.ParseBootloaderState(new Dictionary<string, string>()));
    }

    [Fact]
    public void ParseActiveSlot_ReadsCurrentSlot()
    {
        var variables = new Dictionary<string, string> { ["current-slot"] = "b" };

        Assert.Equal("b", DeviceOutputParser.ParseActiveSlot(variables));
    }

    [Fact]
    public void ParseActiveSlot_MissingKey_ReturnsUnknown()
    {
        Assert.Equal("tidak diketahui", DeviceOutputParser.ParseActiveSlot(new Dictionary<string, string>()));
    }

    [Fact]
    public void ParseActiveSlot_EmptyValue_ReturnsUnknown()
    {
        var variables = new Dictionary<string, string> { ["current-slot"] = "   " };

        Assert.Equal("tidak diketahui", DeviceOutputParser.ParseActiveSlot(variables));
    }
}
