using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Babelserver.DotNet.TestLogger;

[ExtensionUri("logger://Babelserver.DotNet.TestLogger/v1")]
[FriendlyName("list")]
public class ListTestLogger : ITestLoggerWithParameters
{
    private string? _currentTestClass;
    private string? _currentBaseMethodName;
    private int _totalPassed;
    private int _totalFailed;
    private int _totalSkipped;
    private bool _headerPrinted;
    private bool _verbose;

    // Buffer for grouping parameterized tests
    private readonly List<TestResult> _pendingResults = [];

    protected virtual bool DefaultVerbose => false;

    public void Initialize(TestLoggerEvents events, string _) =>
        Initialize(events, new Dictionary<string, string?>());

    public void Initialize(TestLoggerEvents events, Dictionary<string, string?> parameters)
    {
        _verbose = DefaultVerbose || (parameters.TryGetValue("verbose", out var verboseValue)
            && (verboseValue?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false));

        events.TestResult += OnTestResult;
        events.TestRunComplete += OnTestRunComplete;
    }

    private void OnTestResult(object? sender, TestResultEventArgs e)
    {
        if (!_headerPrinted)
        {
            PrintHeader();
            _headerPrinted = true;
        }

        var result = e.Result;
        var testCase = result.TestCase;
        var className = GetClassName(testCase.FullyQualifiedName);
        var baseMethodName = GetBaseMethodName(testCase.FullyQualifiedName);

        // Check if we've moved to a new class or method - flush pending results first
        var classChanged = className != _currentTestClass;
        var methodChanged = baseMethodName != _currentBaseMethodName;

        if ((classChanged || methodChanged) && _pendingResults.Count > 0)
        {
            FlushPendingResults();
        }

        // If this is a new class, print a class header
        if (classChanged)
        {
            _currentTestClass = className;
            Console.WriteLine($"Running {OutputStyle.Cyan}{className}{OutputStyle.Reset}");
        }

        _currentBaseMethodName = baseMethodName;

        if (_verbose)
        {
            PrintSingleResult(result);
        }
        else
        {
            _pendingResults.Add(result);
        }
    }

    private void PrintSingleResult(TestResult result)
    {
        var testName = GetTestName(result.TestCase.DisplayName, result.TestCase.FullyQualifiedName);

        switch (result.Outcome)
        {
            case TestOutcome.Passed:
                Console.WriteLine(OutputStyle.PassedResult(testName, result.Duration));
                _totalPassed++;
                break;

            case TestOutcome.Failed:
                Console.WriteLine(OutputStyle.FailedResult(testName, result.Duration));
                PrintFailureDetails(result);
                _totalFailed++;
                break;

            case TestOutcome.Skipped:
                // Skip reason can be in ErrorMessage (from our adapter) or in Messages
                var reason = result.ErrorMessage
                    ?? result.Messages.FirstOrDefault()?.Text;
                Console.WriteLine(OutputStyle.SkippedResult(testName, reason));
                _totalSkipped++;
                break;
            case TestOutcome.None:
                Console.WriteLine(OutputStyle.UnknownResult(testName, "None"));
                break;

            case TestOutcome.NotFound:
                Console.WriteLine(OutputStyle.UnknownResult(testName, "NotFound"));
                break;

            default:
                Console.WriteLine(OutputStyle.UnknownResult(testName, result.Outcome.ToString()));
                break;
        }
    }

    private void FlushPendingResults()
    {
        if (_pendingResults.Count == 0)
            return;

        var totalRuns = _pendingResults.Count;
        var passed = _pendingResults.Count(r => r.Outcome == TestOutcome.Passed);
        var failed = _pendingResults.Count(r => r.Outcome == TestOutcome.Failed);
        var skipped = _pendingResults.Count(r => r.Outcome == TestOutcome.Skipped);
        var totalDuration = TimeSpan.FromTicks(_pendingResults.Sum(r => r.Duration.Ticks));
        var baseMethodName = _currentBaseMethodName ?? "Unknown";

        // Update totals
        _totalPassed += passed;
        _totalFailed += failed;
        _totalSkipped += skipped + _pendingResults.Count(r =>
            r.Outcome != TestOutcome.Passed &&
            r.Outcome != TestOutcome.Failed &&
            r.Outcome != TestOutcome.Skipped);

        if (totalRuns == 1)
        {
            // Single test - print normally (without run count)
            var result = _pendingResults[0];
            var testName = GetTestName(result.TestCase.DisplayName, result.TestCase.FullyQualifiedName);

            Console.WriteLine(result.Outcome switch
            {
                TestOutcome.Passed => OutputStyle.PassedResult(testName, result.Duration),
                TestOutcome.Failed => OutputStyle.FailedResult(testName, result.Duration),
                TestOutcome.Skipped => OutputStyle.SkippedResult(testName,
                    result.ErrorMessage ?? result.Messages.FirstOrDefault()?.Text),
                _ => OutputStyle.UnknownResult(testName, result.Outcome.ToString())
            });

            if (result.Outcome == TestOutcome.Failed)
            {
                PrintFailureDetails(result);
            }
        }
        else if (failed == 0 && skipped == 0)
        {
            // All passed
            Console.WriteLine(OutputStyle.GroupedPassedResult(baseMethodName, totalRuns, totalDuration));
        }
        else if (failed > 0)
        {
            // Some failed
            Console.WriteLine(OutputStyle.GroupedFailedResult(baseMethodName, failed, totalRuns, totalDuration));

            // Print failure details for each failed test
            foreach (var failedResult in _pendingResults.Where(r => r.Outcome == TestOutcome.Failed))
            {
                var testName = GetTestName(failedResult.TestCase.DisplayName, failedResult.TestCase.FullyQualifiedName);
                Console.WriteLine($"    {OutputStyle.Dim}► {testName}{OutputStyle.Reset}");
                PrintFailureDetails(failedResult);
            }
        }
        else
        {
            // All skipped or mixed skipped/passed
            Console.WriteLine(OutputStyle.GroupedSkippedResult(baseMethodName, skipped, totalRuns));
        }

        _pendingResults.Clear();
    }

    private void OnTestRunComplete(object? sender, TestRunCompleteEventArgs e)
    {
        FlushPendingResults();
        PrintSummary();
    }

    private static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine(OutputStyle.HorizontalLine);
        Console.WriteLine($" {OutputStyle.Bold}T E S T S{OutputStyle.Reset}");
        Console.WriteLine(OutputStyle.HorizontalLine);
    }

    private void PrintSummary()
    {
        var totalTests = _totalPassed + _totalFailed + _totalSkipped;

        Console.WriteLine(OutputStyle.HorizontalLine);

        if (_totalFailed > 0)
        {
            Console.WriteLine($"{OutputStyle.Failed} {OutputStyle.Red}Tests: {totalTests}, " +
                $"Passed: {_totalPassed}, Failed: {_totalFailed}, Skipped: {_totalSkipped}{OutputStyle.Reset}");
        }
        else
        {
            Console.WriteLine($"{OutputStyle.Passed} {OutputStyle.Green}Tests: {totalTests}, " +
                $"Passed: {_totalPassed}, Failed: {_totalFailed}, Skipped: {_totalSkipped}{OutputStyle.Reset}");
        }

        Console.WriteLine(OutputStyle.HorizontalLine);
        Console.WriteLine();
    }

    private static void PrintFailureDetails(TestResult result)
    {
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            Console.WriteLine($"    {OutputStyle.Red}Error: {result.ErrorMessage}{OutputStyle.Reset}");
        }

        if (string.IsNullOrEmpty(result.ErrorStackTrace))
            return;

        var lines = result.ErrorStackTrace.Split('\n').Take(5);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine($"    {OutputStyle.Red}{line.Trim()}{OutputStyle.Reset}");
            }
        }
    }

    private static string GetClassName(string fullyQualifiedName)
    {
        // Format: Namespace.ClassName.MethodName or Namespace.ClassName.MethodName(params)
        var parenIndex = fullyQualifiedName.IndexOf('(');
        var nameWithoutParams = parenIndex >= 0
            ? fullyQualifiedName[..parenIndex]
            : fullyQualifiedName;

        var lastDot = nameWithoutParams.LastIndexOf('.');
        return lastDot > 0 ? nameWithoutParams[..lastDot] : nameWithoutParams;
    }

    private static string GetBaseMethodName(string fullyQualifiedName)
    {
        // Extract just the method name without parameters
        // Format: Namespace.ClassName.MethodName or Namespace.ClassName.MethodName(params)
        var parenIndex = fullyQualifiedName.IndexOf('(');
        var nameWithoutParams = parenIndex >= 0
            ? fullyQualifiedName[..parenIndex]
            : fullyQualifiedName;

        var lastDot = nameWithoutParams.LastIndexOf('.');
        return lastDot > 0 ? nameWithoutParams[(lastDot + 1)..] : nameWithoutParams;
    }

    private static string GetTestName(string displayName, string fullyQualifiedName)
    {
        // If the display name contains the method name, use it (handles Theory data)
        // Otherwise extract just the method name
        var className = GetClassName(fullyQualifiedName);

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (displayName.StartsWith(className + ".", StringComparison.InvariantCulture))
        {
            return displayName[(className.Length + 1)..];
        }

        // For Theory tests, the display name might be "MethodName(param1, param2)"
        return displayName;
    }

}
