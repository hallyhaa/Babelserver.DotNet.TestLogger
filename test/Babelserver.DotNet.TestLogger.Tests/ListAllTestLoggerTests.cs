using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using NSubstitute;

namespace Babelserver.DotNet.TestLogger.Tests;

public class ListTestLoggerTests
{
    [Fact]
    public void ListLogger_GroupsParameterizedTests()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        // Simulate 3 parameterized test results from the same method
        SendTestResult(logger, "Namespace.TestClass.MyTheory(1)", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.TestClass.MyTheory(2)", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.TestClass.MyTheory(3)", TestOutcome.Passed);
        CompleteTestRun(logger);

        var result = output.ToString();

        // Should show grouped output with run count, not individual tests
        Assert.Contains("3 runs", result);
        Assert.DoesNotContain("MyTheory(1)", result);
        Assert.DoesNotContain("MyTheory(2)", result);
        Assert.DoesNotContain("MyTheory(3)", result);
    }

    [Fact]
    public void ListAllLogger_ShowsEachParameterizedTestIndividually()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListAllTestLogger>();

        // Simulate 3 parameterized test results from the same method
        SendTestResult(logger, "Namespace.TestClass.MyTheory(1)", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.TestClass.MyTheory(2)", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.TestClass.MyTheory(3)", TestOutcome.Passed);
        CompleteTestRun(logger);

        var result = output.ToString();

        // Should show each test individually
        Assert.Contains("MyTheory(1)", result);
        Assert.Contains("MyTheory(2)", result);
        Assert.Contains("MyTheory(3)", result);
        Assert.DoesNotContain("3 runs", result);
    }

    [Fact]
    public void ListLogger_ShowsFailureDetails_WhenSomeParameterizedTestsFail()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.TestClass.MyTheory(1)", TestOutcome.Passed);
        SendTestResult(logger, "Namespace.TestClass.MyTheory(2)", TestOutcome.Failed, "Expected 4 but got 5");
        SendTestResult(logger, "Namespace.TestClass.MyTheory(3)", TestOutcome.Passed);
        CompleteTestRun(logger);

        var result = output.ToString();

        Assert.Contains("1/3 runs failed", result);
        Assert.Contains("MyTheory(2)", result); // Failed test should be shown
        Assert.Contains("Expected 4 but got 5", result);
    }

    [Fact]
    public void ListLogger_SingleTest_NoRunCount()
    {
        var (logger, output) = CreateLoggerWithCapturedOutput<ListTestLogger>();

        SendTestResult(logger, "Namespace.TestClass.SingleTest", TestOutcome.Passed);
        CompleteTestRun(logger);

        var result = output.ToString();

        Assert.Contains("SingleTest", result);
        Assert.DoesNotContain("runs", result);
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

    private static void SendTestResult(ListTestLogger logger, string fullyQualifiedName, TestOutcome outcome, string? errorMessage = null)
    {
        var testCase = new TestCase(fullyQualifiedName, new Uri("executor://test"), "test.dll")
        {
            DisplayName = fullyQualifiedName[(fullyQualifiedName.LastIndexOf('.') + 1)..]
        };

        var testResult = new TestResult(testCase)
        {
            Outcome = outcome,
            Duration = TimeSpan.FromMilliseconds(10),
            ErrorMessage = errorMessage
        };

        // Use reflection to call the private OnTestResult method
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
