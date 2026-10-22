using DuckFlash.Models;
using Xunit;

namespace DuckFlash.Tests;

public class SmokeTests
{
    [Fact]
    public void CommandResult_Success_CombinesOutput()
    {
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "ok",
            StandardError = string.Empty
        };

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.CombinedOutput);
    }

    [Theory]
    [InlineData("recovery", true)]
    [InlineData("boot", true)]
    [InlineData("vendor_boot", true)]
    [InlineData("init_boot", true)]
    [InlineData("vbmeta", true)]
    [InlineData("dtbo", true)]
    [InlineData("super", false)]
    [InlineData("", false)]
    public void FlashPartition_OnlyAllowsKnownPartitions(string partition, bool expected)
    {
        Assert.Equal(expected, FlashPartition.IsAllowed(partition));
    }
}
