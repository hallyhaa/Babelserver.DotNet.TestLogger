namespace Babelserver.DotNet.TestLogger.Tests;

public class OutputStyleTests
{

    [Fact]
    public void PassedResult_EndsWithDuration()
    {
        var result = OutputStyle.PassedResult("TestName", TimeSpan.FromMilliseconds(42));

        // Verify structure: name + duration
        Assert.Contains("TestName", result);
        Assert.Contains("42ms", result);
        Assert.EndsWith(OutputStyle.Reset, result);
    }

    [Fact]
    public void FailedResult_UsesRedColor()
    {
        var result = OutputStyle.FailedResult("TestName", TimeSpan.FromMilliseconds(10));

        Assert.Contains(OutputStyle.Red, result);
        // Red should come before the test name
        var redIndex = result.IndexOf(OutputStyle.Red, StringComparison.InvariantCulture);
        var nameIndex = result.IndexOf("TestName", StringComparison.InvariantCulture);
        Assert.True(redIndex < nameIndex, "Red color code should appear before test name");
    }

    [Fact]
    public void SkippedResult_WithNullReason_NoParentheses()
    {
        var result = OutputStyle.SkippedResult("TestName");

        Assert.DoesNotContain("(", result[result.IndexOf("TestName", StringComparison.InvariantCulture)..]);
    }

    [Fact]
    public void SkippedResult_WithEmptyReason_NoParentheses()
    {
        var result = OutputStyle.SkippedResult("TestName", "");

        Assert.DoesNotContain("(", result[result.IndexOf("TestName", StringComparison.InvariantCulture)..]);
    }

    [Fact]
    public void SkippedResult_WithReason_ReasonInDimColor()
    {
        var result = OutputStyle.SkippedResult("TestName", "Database unavailable");

        // Reason should be wrapped in Dim...Reset
        var reasonIndex = result.IndexOf("Database unavailable", StringComparison.InvariantCulture);
        var dimBeforeReason = result.LastIndexOf(OutputStyle.Dim, reasonIndex, StringComparison.InvariantCulture);
        var resetAfterReason = result.IndexOf(OutputStyle.Reset, reasonIndex, StringComparison.InvariantCulture);

        Assert.True(dimBeforeReason >= 0 && dimBeforeReason < reasonIndex, "Dim code should appear before reason");
        Assert.True(resetAfterReason > reasonIndex, "Reset code should appear after reason");
    }

    [Fact]
    public void AllResultMethods_HaveSameIndentation()
    {
        var passed = OutputStyle.PassedResult("Test", TimeSpan.Zero);
        var failed = OutputStyle.FailedResult("Test", TimeSpan.Zero);
        var skipped = OutputStyle.SkippedResult("Test");

        // All should start with 2-space indent
        Assert.StartsWith("  ", passed);
        Assert.StartsWith("  ", failed);
        Assert.StartsWith("  ", skipped);
    }

    [Fact]
    public void GroupedPassedResult_ContainsRunCount()
    {
        var result = OutputStyle.GroupedPassedResult("TestName", 4, TimeSpan.FromMilliseconds(100));

        Assert.Contains("TestName", result);
        Assert.Contains("4 runs", result);
        Assert.Contains("100ms", result);
        Assert.Contains(OutputStyle.Green, result);
    }

    [Fact]
    public void GroupedFailedResult_ContainsFailedCount()
    {
        var result = OutputStyle.GroupedFailedResult("TestName", 2, 4, TimeSpan.FromMilliseconds(50));

        Assert.Contains("TestName", result);
        Assert.Contains("2/4 runs failed", result);
        Assert.Contains("50ms", result);
        Assert.Contains(OutputStyle.Red, result);
    }

    [Fact]
    public void GroupedSkippedResult_ContainsSkippedCount()
    {
        var result = OutputStyle.GroupedSkippedResult("TestName", 3, 5);

        Assert.Contains("TestName", result);
        Assert.Contains("3/5 runs skipped", result);
    }

    [Fact]
    public void GroupedResultMethods_HaveSameIndentation()
    {
        var passed = OutputStyle.GroupedPassedResult("Test", 2, TimeSpan.Zero);
        var failed = OutputStyle.GroupedFailedResult("Test", 1, 2, TimeSpan.Zero);
        var skipped = OutputStyle.GroupedSkippedResult("Test", 1, 2);

        Assert.StartsWith("  ", passed);
        Assert.StartsWith("  ", failed);
        Assert.StartsWith("  ", skipped);
    }

}
