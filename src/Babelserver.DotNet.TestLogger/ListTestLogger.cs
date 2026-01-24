using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Babelserver.DotNet.TestLogger;

[ExtensionUri("logger://Babelserver.DotNet.TestLogger/v1")]
[FriendlyName("list")]
public class ListTestLogger : ITestLoggerWithParameters
{
    private string? _currentTestClass;
    private int _totalPassed;
    private int _totalFailed;
    private int _totalSkipped;
    private bool _headerPrinted;

    public void Initialize(TestLoggerEvents events, string _) =>
        Initialize(events, new Dictionary<string, string?>());

    public void Initialize(TestLoggerEvents events, Dictionary<string, string?> parameters)
    {
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

        // If this is a new class, print a class header
        if (className != _currentTestClass)
        {
            _currentTestClass = className;
            Console.WriteLine($"Running {OutputStyle.Cyan}{className}{OutputStyle.Reset}");
        }

        var testName = GetTestName(testCase.DisplayName, testCase.FullyQualifiedName);

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

    private void OnTestRunComplete(object? sender, TestRunCompleteEventArgs e) =>
        PrintSummary();

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
