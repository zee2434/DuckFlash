using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsSuccessAndOutput()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "echo", "hello" },
            default);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task RunAsync_BuildsCommandLineForPreview()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "echo", "hello" },
            default);

        Assert.Equal("cmd.exe /c echo hello", result.CommandLine);
    }

    [Fact]
    public async Task RunAsync_RaisesCommandStartingBeforeProcessRuns()
    {
        var runner = new ProcessRunner();
        string? previewed = null;
        runner.CommandStarting += (_, e) => previewed = e.CommandLine;

        await runner.RunAsync("cmd.exe", new[] { "/c", "echo", "hi" }, default);

        Assert.Equal("cmd.exe /c echo hi", previewed);
    }

    [Fact]
    public async Task RunAsync_RaisesOutputReceivedPerLine()
    {
        var runner = new ProcessRunner();
        var lines = new List<string>();
        runner.OutputReceived += (_, e) => lines.Add(e.Line);

        await runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "echo", "satu", "&", "echo", "dua" },
            default);

        // cmd.exe /c echo menyisakan spasi di akhir baris, jadi bandingkan setelah di-trim.
        Assert.Contains(lines, line => line.Trim() == "satu");
        Assert.Contains(lines, line => line.Trim() == "dua");
    }

    [Fact]
    public async Task RunAsync_CapturesStandardErrorSeparately()
    {
        var runner = new ProcessRunner();
        var errorLines = new List<string>();
        runner.OutputReceived += (_, e) =>
        {
            if (e.Source == ProcessOutputSource.StandardError)
            {
                errorLines.Add(e.Line);
            }
        };

        // fastboot menulis output ke stderr, jadi stderr wajib tertangkap.
        var result = await runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "echo ke_salah 1>&2" },
            default);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(errorLines, line => line.Trim() == "ke_salah");
        Assert.Contains("ke_salah", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_IsReported()
    {
        var runner = new ProcessRunner();

        var result = await runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "exit 3" },
            default);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_MissingExecutable_ThrowsProcessStartException()
    {
        var runner = new ProcessRunner();

        await Assert.ThrowsAsync<ProcessStartException>(() =>
            runner.RunAsync("tidak_ada_executable_duckflash_12345.exe", Array.Empty<string>(), default));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_StopsLongRunningProcess()
    {
        using var cts = new CancellationTokenSource();
        var runner = new ProcessRunner();

        // Proses sengaja dibuat lama; token dibatalkan setelah 300 ms.
        var run = runner.RunAsync(
            "cmd.exe",
            new[] { "/c", "ping", "127.0.0.1", "-n", "30", ">nul" },
            cts.Token);

        await Task.Delay(300);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public async Task RunAsync_EmptyFileName_Throws()
    {
        var runner = new ProcessRunner();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunAsync("  ", Array.Empty<string>(), default));
    }

    [Theory]
    [InlineData("plainarg", "plainarg")]
    [InlineData("", "\"\"")]
    public void QuoteArgument_SimpleValues(string input, string expected)
    {
        Assert.Equal(expected, ProcessRunner.QuoteArgument(input));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has\"quote")]
    public void QuoteArgument_SpacesOrQuotesGetWrapped(string input)
    {
        var quoted = ProcessRunner.QuoteArgument(input);

        Assert.StartsWith("\"", quoted);
        Assert.EndsWith("\"", quoted);
    }

    [Fact]
    public void QuoteArgument_EscapesInnerQuote()
    {
        var quoted = ProcessRunner.QuoteArgument("a\"b");

        Assert.Equal("\"a\\\"b\"", quoted);
    }
}
