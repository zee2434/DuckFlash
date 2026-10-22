using DuckFlash.Models;
using Xunit;

namespace DuckFlash.Tests;

public class LogEntryTests
{
    [Fact]
    public void Formatted_ContainsTimestampAndMessage()
    {
        var entry = new LogEntry("Halo dunia");

        Assert.Contains("Halo dunia", entry.Formatted);
        Assert.Contains(entry.Timestamp.ToString("HH:mm:ss"), entry.Formatted);
    }

    [Fact]
    public void Severity_DefaultsToInfo()
    {
        var entry = new LogEntry("Pesan biasa");

        Assert.Equal(LogSeverity.Info, entry.Severity);
    }

    [Theory]
    [InlineData(LogSeverity.Info)]
    [InlineData(LogSeverity.Success)]
    [InlineData(LogSeverity.Warning)]
    [InlineData(LogSeverity.Error)]
    public void Severity_IsPreserved(LogSeverity severity)
    {
        var entry = new LogEntry("Pesan", severity);

        Assert.Equal(severity, entry.Severity);
    }
}
