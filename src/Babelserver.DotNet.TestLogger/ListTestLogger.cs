using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Babelserver.DotNet.TestLogger;

[ExtensionUri("logger://Babelserver.DotNet.TestLogger/v1")]
[FriendlyName("list")]
public class ListTestLogger : ITestLoggerWithParameters
{
    private int _totalPassed;
    private int _totalFailed;
    private int _totalSkipped;
    private bool _verbose;

    // Buffer for grouping parameterized tests
    private readonly List<TestResult> _pendingResults = [];

    protected virtual bool DefaultVerbose => false;

    public void Initialize(TestLoggerEvents events, string testRunDirectory) =>
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
        // Buffer all results - we'll group and print them in OnTestRunComplete
        // This handles parallel test execution where results arrive out of order
        _pendingResults.Add(e.Result);
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
                _totalSkipped++;
                break;

            case TestOutcome.NotFound:
                Console.WriteLine(OutputStyle.UnknownResult(testName, "NotFound"));
                _totalSkipped++;
                break;

            default:
                Console.WriteLine(OutputStyle.UnknownResult(testName, result.Outcome.ToString()));
                _totalSkipped++;
                break;
        }
    }

    private void OnTestRunComplete(object? sender, TestRunCompleteEventArgs e)
    {
        PrintHeader();
        PrintAllResults();
        PrintSummary();
    }

    private void PrintAllResults()
    {
        // Group results by class, then by base method name
        var resultsByClass = _pendingResults
            .GroupBy(r => GetClassName(r.TestCase.FullyQualifiedName))
            .OrderBy(g => g.Key);

        var firstClass = true;
        foreach (var classGroup in resultsByClass)
        {
            if (!firstClass)
            {
                Console.WriteLine();
            }
            firstClass = false;
            Console.WriteLine($"Running {OutputStyle.Cyan}{classGroup.Key}{OutputStyle.Reset}");

            var resultsByMethod = classGroup
                .GroupBy(r => GetBaseMethodName(r.TestCase.FullyQualifiedName))
                .OrderBy(g => g.Key);

            foreach (var methodGroup in resultsByMethod)
            {
                var results = methodGroup.ToList();

                if (_verbose)
                {
                    foreach (var result in results)
                    {
                        PrintSingleResult(result);
                    }
                }
                else
                {
                    PrintGroupedResults(results);
                }
            }
        }
    }

    private void PrintGroupedResults(List<TestResult> results)
    {
        if (results.Count == 0)
            return;

        var totalRuns = results.Count;
        var passed = results.Count(r => r.Outcome == TestOutcome.Passed);
        var failed = results.Count(r => r.Outcome == TestOutcome.Failed);
        var skipped = results.Count(r => r.Outcome == TestOutcome.Skipped);
        var totalDuration = TimeSpan.FromTicks(results.Sum(r => r.Duration.Ticks));
        var baseMethodName = GetBaseMethodName(results[0].TestCase.FullyQualifiedName);

        // Update totals
        _totalPassed += passed;
        _totalFailed += failed;
        _totalSkipped += skipped + results.Count(r =>
            r.Outcome != TestOutcome.Passed &&
            r.Outcome != TestOutcome.Failed &&
            r.Outcome != TestOutcome.Skipped);

        if (totalRuns == 1)
        {
            // Single test - print normally (without run count)
            var result = results[0];
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
            foreach (var failedResult in results.Where(r => r.Outcome == TestOutcome.Failed))
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
