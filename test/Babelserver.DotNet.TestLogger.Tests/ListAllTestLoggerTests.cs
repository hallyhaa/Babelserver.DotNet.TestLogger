using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NSubstitute;

namespace Babelserver.DotNet.TestLogger.Tests;

public class ListTestLoggerTests
{
    [Fact]
    public void FirstResult_BecomesActiveAndPrintsImmediately()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 1);

        var result = output.ToString();
        Assert.Contains("T E S T S", result);
        Assert.Contains("Namespace.ClassA", result);
        Assert.Contains("Test1", result);
    }

    [Fact]
    public void ActiveClass_ResultsPrintImmediately()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Passed, classTestCount: 2);

        var result = output.ToString();
        Assert.Contains("Test1", result);
        Assert.Contains("Test2", result);
    }

    [Fact]
    public void NonActiveClass_ResultsAreBuffered()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Passed, classTestCount: 1);

        var result = output.ToString();
        Assert.Contains("Namespace.ClassA", result);
        Assert.DoesNotContain("Namespace.ClassB", result);
    }

    [Fact]
    public void ActiveClassComplete_SwitchesToBufferedClass()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // ClassA has 1 test, ClassB has 2
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 1);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test2", TestOutcome.Passed, classTestCount: 2);

        // ClassA completed on its first result (count=1), so it switches to ClassB
        var result = output.ToString();
        Assert.Contains("Namespace.ClassB", result);
    }

    [Fact]
    public void CompletedBufferedClasses_FlushedBeforeNewActive()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // ClassA: 2 tests (active), ClassB: 1 test (buffered, will complete), ClassC: 1 test (buffered)
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Passed, classTestCount: 1);
        SendTestResult(logger, "Namespace.ClassC.Test1", TestOutcome.Passed, classTestCount: 2);

        // ClassB is already complete (1/1). When ClassA completes, ClassB flushes first.
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Passed, classTestCount: 2);

        var result = output.ToString();
        Assert.Contains("Namespace.ClassB", result);
        Assert.Contains("Namespace.ClassC", result);

        // ClassB (completed) should appear before ClassC (new active)
        var posB = result.IndexOf("Namespace.ClassB", StringComparison.Ordinal);
        var posC = result.IndexOf("Namespace.ClassC", StringComparison.Ordinal);
        Assert.True(posB < posC);
    }

    [Fact]
    public void NewActiveClass_StreamsFutureResults()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // ClassA: 1 test, ClassB: 2 tests
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 1);
        // ClassA done → ClassB is empty buffer, no switch yet
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Passed, classTestCount: 2);

        // ClassB is now active, Test2 should print immediately
        SendTestResult(logger, "Namespace.ClassB.Test2", TestOutcome.Passed, classTestCount: 2);

        var result = output.ToString();
        Assert.Contains("Test2", result);
    }

    [Fact]
    public void OnComplete_FlushesRemainingBuffers()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Failed, classTestCount: 1, errorMessage: "boom");
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("Namespace.ClassA", result);
        Assert.Contains("Namespace.ClassB", result);
        Assert.Contains("boom", result);
        Assert.Contains("Tests: 2", result);
    }

    [Fact]
    public void FallbackWithoutProperty_FlushesOnComplete()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // No classTestCount → property not set → fallback
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Passed);

        // ClassB buffered, no class count known → stays buffered
        Assert.DoesNotContain("Namespace.ClassB", output.ToString());

        CompleteTestRun(logger);
        Assert.Contains("Namespace.ClassB", output.ToString());
    }

    [Fact]
    public void ClassHeaderOnlyPrintedOnce()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Passed, classTestCount: 2);
        CompleteTestRun(logger);

        var result = output.ToString();
        var firstPos = result.IndexOf("Namespace.ClassA", StringComparison.Ordinal);
        var secondPos = result.IndexOf("Namespace.ClassA", firstPos + 1, StringComparison.Ordinal);
        Assert.Equal(-1, secondPos);
    }

    [Fact]
    public void TheoryResults_GroupedWithRunCount()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 1)", TestOutcome.Passed, classTestCount: 3);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 2)", TestOutcome.Passed, classTestCount: 3);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 3)", TestOutcome.Passed, classTestCount: 3);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("3 runs", result);
        // Individual parameters should not appear as separate test lines
        Assert.DoesNotContain("x: 1", result);
        Assert.DoesNotContain("x: 2", result);
    }

    [Fact]
    public void TheoryResults_FailuresShowDetails()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 1)", TestOutcome.Passed, classTestCount: 3);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 2)", TestOutcome.Failed, classTestCount: 3, errorMessage: "bad value");
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 3)", TestOutcome.Passed, classTestCount: 3);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("1/3 runs failed", result);
        // Failed run details should be shown
        Assert.Contains("bad value", result);
        Assert.Contains("x: 2", result);
    }

    [Fact]
    public void TheoryAndFact_MixedInSameClass()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.FactTest", TestOutcome.Passed, classTestCount: 4);
        SendTestResult(logger, "Namespace.ClassA.TheoryMethod(x: 1)", TestOutcome.Passed, classTestCount: 4);
        SendTestResult(logger, "Namespace.ClassA.TheoryMethod(x: 2)", TestOutcome.Passed, classTestCount: 4);
        SendTestResult(logger, "Namespace.ClassA.TheoryMethod(x: 3)", TestOutcome.Passed, classTestCount: 4);
        CompleteTestRun(logger);

        var result = output.ToString();
        // Fact should appear individually
        Assert.Contains("FactTest", result);
        // Theory should be grouped
        Assert.Contains("3 runs", result);
        Assert.Contains("Tests: 4", result);
    }

    [Fact]
    public void CollapseTheoriesFalse_ShowsEachRunIndividually()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 1)", TestOutcome.Passed, classTestCount: 3, collapseTheories: false);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 2)", TestOutcome.Passed, classTestCount: 3, collapseTheories: false);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 3)", TestOutcome.Passed, classTestCount: 3, collapseTheories: false);
        CompleteTestRun(logger);

        var result = output.ToString();
        // Each parameterized run should appear individually
        Assert.Contains("x: 1", result);
        Assert.Contains("x: 2", result);
        Assert.Contains("x: 3", result);
        // Should NOT show grouped "runs" output
        Assert.DoesNotContain("runs", result);
    }

    [Fact]
    public void CollapseTheoriesTrue_GroupsRuns()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 1)", TestOutcome.Passed, classTestCount: 3, collapseTheories: true);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 2)", TestOutcome.Passed, classTestCount: 3, collapseTheories: true);
        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 3)", TestOutcome.Passed, classTestCount: 3, collapseTheories: true);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("3 runs", result);
        Assert.DoesNotContain("x: 1", result);
    }

    [Fact]
    public void ConsecutiveTheories_NoBlankLinesBetween()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // Two consecutive theories in the same class
        SendTestResult(logger, "Namespace.ClassA.TheoryA(x: 1)", TestOutcome.Passed, classTestCount: 5);
        SendTestResult(logger, "Namespace.ClassA.TheoryA(x: 2)", TestOutcome.Passed, classTestCount: 5);
        SendTestResult(logger, "Namespace.ClassA.TheoryB(y: 1)", TestOutcome.Passed, classTestCount: 5);
        SendTestResult(logger, "Namespace.ClassA.TheoryB(y: 2)", TestOutcome.Passed, classTestCount: 5);
        SendTestResult(logger, "Namespace.ClassA.TheoryB(y: 3)", TestOutcome.Passed, classTestCount: 5);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("2 runs", result);  // TheoryA
        Assert.Contains("3 runs", result);  // TheoryB

        // Strip ANSI escape codes to get visible lines
        var visibleLines = GetVisibleLines(result);

        // Find the two theory result lines
        var theoryALine = visibleLines.FindIndex(l => l.Contains("TheoryA") && l.Contains("2 runs"));
        var theoryBLine = visibleLines.FindIndex(l => l.Contains("TheoryB") && l.Contains("3 runs"));

        Assert.True(theoryALine >= 0, "TheoryA grouped line should exist");
        Assert.True(theoryBLine >= 0, "TheoryB grouped line should exist");
        Assert.Equal(theoryALine + 1, theoryBLine); // Adjacent, no blank line between
    }

    /// <summary>
    /// Strips ANSI escape codes and returns non-empty visible lines.
    /// </summary>
    private static List<string> GetVisibleLines(string rawOutput)
    {
        var stripped = System.Text.RegularExpressions.Regex.Replace(rawOutput, @"\e\[[^A-Za-z]*[A-Za-z]", "");
        return stripped.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    [Fact]
    public void FailingBufferedClass_DeferredToEnd()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // ClassA: active (2 tests), ClassB: buffered with failure (1 test), ClassC: buffered passing (1 test)
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Failed, classTestCount: 1, errorMessage: "boom");
        SendTestResult(logger, "Namespace.ClassC.Test1", TestOutcome.Passed, classTestCount: 1);

        // Complete ClassA → should flush ClassC (passing) before ClassB (failing)
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Passed, classTestCount: 2);
        CompleteTestRun(logger);

        var result = output.ToString();
        var posC = result.IndexOf("Namespace.ClassC", StringComparison.Ordinal);
        var posB = result.IndexOf("Namespace.ClassB", StringComparison.Ordinal);
        Assert.True(posC > 0, "ClassC should appear in output");
        Assert.True(posB > 0, "ClassB should appear in output");
        Assert.True(posC < posB, "Passing ClassC should appear before failing ClassB");
    }

    [Fact]
    public void MultipleFailingClasses_AllDeferredToEnd()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // ClassA: active (2 tests), ClassB: failing, ClassC: passing, ClassD: failing — all buffered
        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2);
        SendTestResult(logger, "Namespace.ClassB.Test1", TestOutcome.Failed, classTestCount: 1, errorMessage: "fail1");
        SendTestResult(logger, "Namespace.ClassC.Test1", TestOutcome.Passed, classTestCount: 1);
        SendTestResult(logger, "Namespace.ClassD.Test1", TestOutcome.Failed, classTestCount: 1, errorMessage: "fail2");
        // Complete ClassA → triggers flush of buffered classes
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Passed, classTestCount: 2);
        CompleteTestRun(logger);

        var result = output.ToString();
        var posC = result.IndexOf("Namespace.ClassC", StringComparison.Ordinal);
        var posB = result.IndexOf("Namespace.ClassB", StringComparison.Ordinal);
        var posD = result.IndexOf("Namespace.ClassD", StringComparison.Ordinal);

        // Passing ClassC before failing classes (B, D)
        Assert.True(posC > 0, "ClassC should appear in output");
        Assert.True(posC < posB, "Passing ClassC should appear before failing ClassB");
        Assert.True(posC < posD, "Passing ClassC should appear before failing ClassD");
    }

    [Fact]
    public void ShowTestListFalse_SuppressesAllOutput()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 2, showTestList: false);
        SendTestResult(logger, "Namespace.ClassA.Test2", TestOutcome.Failed, classTestCount: 2, errorMessage: "boom", showTestList: false);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.DoesNotContain("T E S T S", result);
        Assert.DoesNotContain("Namespace.ClassA", result);
        Assert.DoesNotContain("Test1", result);
        Assert.DoesNotContain("boom", result);
    }

    [Fact]
    public void ShowTestListTrue_ShowsOutput()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.Test1", TestOutcome.Passed, classTestCount: 1, showTestList: true);
        CompleteTestRun(logger);

        var result = output.ToString();
        Assert.Contains("T E S T S", result);
        Assert.Contains("Test1", result);
    }

    [Fact]
    public void TheoryResults_PrintedOnlyOnceWhenComplete()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 1)", TestOutcome.Passed, classTestCount: 3);
        // During accumulation, no output yet for the theory
        Assert.DoesNotContain("runs", output.ToString());

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 2)", TestOutcome.Passed, classTestCount: 3);
        Assert.DoesNotContain("runs", output.ToString());

        SendTestResult(logger, "Namespace.ClassA.TestMethod(x: 3)", TestOutcome.Passed, classTestCount: 3);
        CompleteTestRun(logger);
        // After completion, the grouped result appears exactly once
        Assert.Contains("3 runs", output.ToString());
    }

    private static (T logger, StringWriter output) CreateLoggerWithCapturedOutput<T>() where T : ListTestLogger, new()
    {
        var output = new StringWriter();
        Console.SetOut(output);

        var logger = new T();
        var events = Substitute.For<TestLoggerEvents>();
        logger.Initialize(events, new Dictionary<string, string?>());

        return (logger, output);
    }

    private static void SendTestResult(ListTestLogger logger, string fullyQualifiedName, TestOutcome outcome,
        int classTestCount = 0, string? errorMessage = null, bool collapseTheories = true, bool showTestList = true)
    {
        var testCase = new TestCase(fullyQualifiedName, new Uri("executor://test"), "test.dll")
        {
            DisplayName = fullyQualifiedName[(fullyQualifiedName.LastIndexOf('.') + 1)..]
        };

        if (classTestCount > 0)
            testCase.SetPropertyValue(ListTestLogger.ClassTestCountProperty, classTestCount);
        testCase.SetPropertyValue(ListTestLogger.CollapseTheoriesProperty, collapseTheories);
        testCase.SetPropertyValue(ListTestLogger.ShowTestListProperty, showTestList);

        var testResult = new TestResult(testCase)
        {
            Outcome = outcome,
            Duration = TimeSpan.FromMilliseconds(10),
            ErrorMessage = errorMessage
        };

        var method = typeof(ListTestLogger).GetMethod("OnTestResult",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(logger, [null, new TestResultEventArgs(testResult)]);
    }

    private static void CompleteTestRun(ListTestLogger logger)
    {
        var method = typeof(ListTestLogger).GetMethod("OnTestRunComplete",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var stats = Substitute.For<ITestRunStatistics>();
        var args = new TestRunCompleteEventArgs(stats, false, false, null, null, TimeSpan.Zero);
        method!.Invoke(logger, [null, args]);
    }
}
