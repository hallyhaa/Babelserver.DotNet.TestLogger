using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Babelserver.DotNet.TestLogger;

[ExtensionUri("logger://Babelserver.DotNet.TestLogger/v1")]
[FriendlyName("list")]
public class ListTestLogger : ITestLoggerWithParameters
{
    /// <summary>
    /// TestProperty set by the adapter on each TestCase, containing the total
    /// number of tests in that test class. Used to detect class completion.
    /// </summary>
    public static readonly TestProperty ClassTestCountProperty = TestProperty.Register(
        "BabelserverClassTestCount", "Babelserver Class Test Count",
        typeof(int), typeof(ListTestLogger));

    public static readonly TestProperty CollapseTheoriesProperty = TestProperty.Register(
        "BabelserverCollapseTheories", "Babelserver Collapse Theories",
        typeof(bool), typeof(ListTestLogger));

    public static readonly TestProperty ShowTestListProperty = TestProperty.Register(
        "BabelserverShowTestList", "Babelserver Show Test List",
        typeof(bool), typeof(ListTestLogger));

    private int _totalPassed;
    private int _totalFailed;
    private int _totalSkipped;

    // Streaming state
    private string? _activeClass;
    private readonly Dictionary<string, List<TestResult>> _buffer = new();
    private readonly Dictionary<string, int> _expectedCountByClass = new();
    private readonly Dictionary<string, int> _receivedCountByClass = new();
    private readonly HashSet<string> _completedClasses = [];
    private readonly HashSet<string> _classHeaderPrinted = [];
    private bool _headerPrinted;
    private bool? _showTestList;
    private TextWriter _output = Console.Out;
    private TextWriter? _originalError;
    private string _showTestOutput = "onfailure";
    private readonly object _lock = new();

    // Theory grouping state
    private string? _currentTheoryMethod;
    private int _theoryRunCount;
    private int _theoryFailCount;
    private int _theorySkipCount;
    private TimeSpan _theoryDuration;
    private readonly List<TestResult> _theoryFailures = [];

    public void Initialize(TestLoggerEvents events, string testRunDirectory) =>
        Initialize(events, new Dictionary<string, string?>());

    public void Initialize(TestLoggerEvents events, Dictionary<string, string?> parameters)
    {
        _output = Console.Out;

        var suppress = !parameters.TryGetValue("SuppressConsoleOutput", out var value)
                       || !bool.TryParse(value, out var parsed)
                       || parsed;

        if (suppress)
        {
            _originalError = Console.Error;
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }

        if (parameters.TryGetValue("ShowTestOutput", out var outputMode) && outputMode != null)
            _showTestOutput = outputMode.ToLowerInvariant();

        events.TestResult += OnTestResult;
        events.TestRunComplete += OnTestRunComplete;
    }

    private void OnTestResult(object? sender, TestResultEventArgs e)
    {
        lock (_lock)
        {
            _showTestList ??= e.Result.TestCase.GetPropertyValue(ShowTestListProperty, true);

            if (!_showTestList.Value)
            {
                CountResult(e.Result);
                return;
            }

            if (!_headerPrinted)
            {
                PrintHeader();
                _headerPrinted = true;
            }

            var className = GetClassName(e.Result.TestCase.FullyQualifiedName);

            // Learn expected count from the TestProperty (set by adapter)
            if (!_expectedCountByClass.ContainsKey(className))
            {
                var count = e.Result.TestCase.GetPropertyValue(ClassTestCountProperty, 0);
                if (count > 0)
                    _expectedCountByClass[className] = count;
            }

            _receivedCountByClass.TryGetValue(className, out var prevCount);
            _receivedCountByClass[className] = prevCount + 1;

            if (_activeClass == null)
            {
                _activeClass = className;
                if (_classHeaderPrinted.Count > 0)
                    _output.WriteLine();
                PrintClassHeader(className);
                HandleResult(e.Result);
            }
            else if (className == _activeClass)
            {
                // Result for active class — print immediately
                HandleResult(e.Result);
            }
            else
            {
                // Result for non-active class — buffer it
                if (!_buffer.TryGetValue(className, out var list))
                {
                    list = [];
                    _buffer[className] = list;
                }
                list.Add(e.Result);
            }

            // ReSharper disable once InvertIf
            if (IsClassComplete(className))
            {
                // Check if this class just completed
                _completedClasses.Add(className);

                if (className == _activeClass)
                    SwitchActiveClass();
            }
        }
    }

    private bool IsClassComplete(string className) =>
        _expectedCountByClass.TryGetValue(className, out var expected)
        && _receivedCountByClass.TryGetValue(className, out var received)
        && received >= expected;

    private void SwitchActiveClass()
    {
        FinalizeCurrentTheory();

        // First, flush any completed buffered classes that have no failures
        var flushed = true;
        while (flushed)
        {
            flushed = false;
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var cls in _buffer.Keys.ToList())
            {
                if (!_completedClasses.Contains(cls))
                    continue;
                if (BufferHasFailures(cls))
                    continue;

                FlushBufferedClass(cls);
                flushed = true;
            }
        }

        // Pick a new active class: prefer classes without failures, then most buffered results
        var bestClass = PickBestBufferedClass(preferNoFailures: true);

        if (bestClass == null)
        {
            _activeClass = null;
            return;
        }

        _activeClass = bestClass;

        _output.WriteLine();
        PrintClassHeader(bestClass);

        // Print buffered results and continue streaming
        foreach (var result in _buffer[bestClass])
            HandleResult(result);
        _buffer.Remove(bestClass);
    }

    private static bool BufferHasFailures(string className, Dictionary<string, List<TestResult>> buffer) =>
        buffer.TryGetValue(className, out var results)
        && results.Exists(r => r.Outcome == TestOutcome.Failed);

    private bool BufferHasFailures(string className) =>
        BufferHasFailures(className, _buffer);

    private string? PickBestBufferedClass(bool preferNoFailures)
    {
        string? bestClass = null;
        var bestCount = 0;
        var bestHasFailures = true;

        foreach (var kvp in _buffer)
        {
            var hasFailures = kvp.Value.Exists(r => r.Outcome == TestOutcome.Failed);

            if (preferNoFailures && bestClass != null)
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (hasFailures && !bestHasFailures)
                {
                    continue;
                }
                // If this one has no failures but best does, take it
                if (!hasFailures && bestHasFailures)
                {
                    bestClass = kvp.Key;
                    bestCount = kvp.Value.Count;
                    bestHasFailures = false;
                    continue;
                }
            }

            // ReSharper disable once InvertIf
            if (kvp.Value.Count > bestCount)
            {
                bestCount = kvp.Value.Count;
                bestClass = kvp.Key;
                bestHasFailures = hasFailures;
            }
        }

        return bestClass;
    }

    private void FlushBufferedClass(string className)
    {
        _output.WriteLine();
        PrintClassHeader(className);
        foreach (var result in _buffer[className])
            HandleResult(result);
        FinalizeCurrentTheory();
        _buffer.Remove(className);
    }

    private void PrintClassHeader(string className)
    {
        if (!_classHeaderPrinted.Add(className))
            return;

        _output.WriteLine($"Running {OutputStyle.Cyan}{className}{OutputStyle.Reset}");
    }

    private void OnTestRunComplete(object? sender, TestRunCompleteEventArgs e)
    {
        lock (_lock)
        {
            if (_showTestList == false)
            {
                RestoreConsole();
                return;
            }

            if (!_headerPrinted)
            {
                PrintHeader();
                _headerPrinted = true;
            }

            FinalizeCurrentTheory();

            // Flush remaining buffered classes: passing first, then failing
            var passingClasses = _buffer.Where(kvp => !kvp.Value.Exists(r => r.Outcome == TestOutcome.Failed))
                .OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key).ToList();
            var failingClasses = _buffer.Where(kvp => kvp.Value.Exists(r => r.Outcome == TestOutcome.Failed))
                .OrderBy(kvp => kvp.Key).Select(kvp => kvp.Key).ToList();

            foreach (var className in passingClasses.Concat(failingClasses))
                FlushBufferedClass(className);
            _buffer.Clear();

            PrintSummary();
            RestoreConsole();
        }
    }

    private void RestoreConsole()
    {
        if (_originalError == null)
            return;

        Console.SetOut(_output);
        Console.SetError(_originalError);
        _originalError = null;
    }

    private void CountResult(TestResult result)
    {
        switch (result.Outcome)
        {
            case TestOutcome.Passed:
                _totalPassed++;
                break;
            case TestOutcome.Failed:
                _totalFailed++;
                break;
            default:
                _totalSkipped++;
                break;
        }
    }

    private void HandleResult(TestResult result)
    {
        var testName = GetTestName(result.TestCase.DisplayName, result.TestCase.FullyQualifiedName);
        var collapseTheories = result.TestCase.GetPropertyValue(CollapseTheoriesProperty, true);
        var baseMethod = collapseTheories ? GetBaseMethodName(testName) : null;

        if (baseMethod != null)
        {
            if (baseMethod != _currentTheoryMethod)
            {
                // Start new theory
                FinalizeCurrentTheory();
                _currentTheoryMethod = baseMethod;
                _theoryRunCount = 0;
                _theoryFailCount = 0;
                _theorySkipCount = 0;
                _theoryDuration = TimeSpan.Zero;
                _theoryFailures.Clear();
            }

            AccumulateTheoryRun(result);
        }
        else
        {
            FinalizeCurrentTheory();
            PrintSingleResult(result);
        }
    }

    private void AccumulateTheoryRun(TestResult result)
    {
        _theoryRunCount++;
        _theoryDuration += result.Duration;

        switch (result.Outcome)
        {
            case TestOutcome.Passed:
                _totalPassed++;
                break;
            case TestOutcome.Failed:
                _theoryFailCount++;
                _theoryFailures.Add(result);
                _totalFailed++;
                break;
            default:
                _theorySkipCount++;
                _totalSkipped++;
                break;
        }
    }

    private void FinalizeCurrentTheory()
    {
        if (_currentTheoryMethod == null) return;

        // Print the final grouped result line
        string line;
        if (_theoryFailCount > 0)
            line = OutputStyle.GroupedFailedResult(_currentTheoryMethod, _theoryFailCount, _theoryRunCount, _theoryDuration);
        else if (_theorySkipCount > 0)
            line = OutputStyle.GroupedSkippedResult(_currentTheoryMethod, _theorySkipCount, _theoryRunCount);
        else
            line = OutputStyle.GroupedPassedResult(_currentTheoryMethod, _theoryRunCount, _theoryDuration);

        _output.WriteLine(line);

        // Print failure details for failed theory runs
        foreach (var failure in _theoryFailures)
        {
            var testName = GetTestName(failure.TestCase.DisplayName, failure.TestCase.FullyQualifiedName);
            _output.WriteLine($"    {OutputStyle.Red}{testName}{OutputStyle.Reset}");
            if (_showTestOutput is "onfailure" or "always")
                PrintTestOutput(failure);
            PrintFailureDetails(failure);
        }

        _currentTheoryMethod = null;
        _theoryFailures.Clear();
    }

    private void PrintSingleResult(TestResult result)
    {
        var testName = GetTestName(result.TestCase.DisplayName, result.TestCase.FullyQualifiedName);

        switch (result.Outcome)
        {
            case TestOutcome.Passed:
                _output.WriteLine(OutputStyle.PassedResult(testName, result.Duration));
                if (_showTestOutput == "always")
                    PrintTestOutput(result);
                _totalPassed++;
                break;

            case TestOutcome.Failed:
                _output.WriteLine(OutputStyle.FailedResult(testName, result.Duration));
                if (_showTestOutput is "onfailure" or "always")
                    PrintTestOutput(result);
                PrintFailureDetails(result);
                _totalFailed++;
                break;

            case TestOutcome.Skipped:
                var reason = result.ErrorMessage
                    ?? result.Messages.FirstOrDefault()?.Text;
                _output.WriteLine(OutputStyle.SkippedResult(testName, reason));
                _totalSkipped++;
                break;
            case TestOutcome.None:
                _output.WriteLine(OutputStyle.UnknownResult(testName, "None"));
                _totalSkipped++;
                break;

            case TestOutcome.NotFound:
                _output.WriteLine(OutputStyle.UnknownResult(testName, "NotFound"));
                _totalSkipped++;
                break;

            default:
                _output.WriteLine(OutputStyle.UnknownResult(testName, result.Outcome.ToString()));
                _totalSkipped++;
                break;
        }
    }

    private void PrintHeader()
    {
        _output.WriteLine();
        _output.WriteLine(OutputStyle.HorizontalLine);
        _output.WriteLine($" {OutputStyle.Bold}T E S T S{OutputStyle.Reset}");
        _output.WriteLine(OutputStyle.HorizontalLine);
    }

    private void PrintSummary()
    {
        var totalTests = _totalPassed + _totalFailed + _totalSkipped;

        _output.WriteLine(OutputStyle.HorizontalLine);

        if (_totalFailed > 0)
        {
            _output.WriteLine($"{OutputStyle.Failed} {OutputStyle.Red}Tests: {totalTests}, " +
                $"Passed: {_totalPassed}, Failed: {_totalFailed}, Skipped: {_totalSkipped}{OutputStyle.Reset}");
        }
        else
        {
            _output.WriteLine($"{OutputStyle.Passed} {OutputStyle.Green}Tests: {totalTests}, " +
                $"Passed: {_totalPassed}, Failed: {_totalFailed}, Skipped: {_totalSkipped}{OutputStyle.Reset}");
        }

        _output.WriteLine(OutputStyle.HorizontalLine);
        _output.WriteLine();
    }

    private void PrintFailureDetails(TestResult result)
    {
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            _output.WriteLine($"    {OutputStyle.Red}Error: {result.ErrorMessage}{OutputStyle.Reset}");
        }

        if (string.IsNullOrEmpty(result.ErrorStackTrace))
            return;

        var lines = result.ErrorStackTrace?.Split('\n').Take(5) ?? [];
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _output.WriteLine($"    {OutputStyle.Red}{line.Trim()}{OutputStyle.Reset}");
            }
        }
    }

    private void PrintTestOutput(TestResult result)
    {
        var messages = result.Messages
            .Where(m => m.Category == TestResultMessage.StandardOutCategory && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!)
            .ToList();

        if (messages.Count == 0)
            return;

        _output.WriteLine($"    {OutputStyle.Dim}Output:{OutputStyle.Reset}");
        foreach (var text in messages)
        {
            foreach (var line in text.TrimEnd().Split('\n'))
                _output.WriteLine($"    {OutputStyle.Dim}  {line.TrimEnd('\r')}{OutputStyle.Reset}");
        }
    }

    private static string GetClassName(string fullyQualifiedName)
    {
        // Format: Namespace.ClassName.MethodName or Namespace.ClassName.MethodName(params)
        var parenIndex = fullyQualifiedName.IndexOf('(');
        var nameWithoutParams = parenIndex >= 0
            ? fullyQualifiedName.Substring(0, parenIndex)
            : fullyQualifiedName;

        var lastDot = nameWithoutParams.LastIndexOf('.');
        return lastDot > 0 ? nameWithoutParams.Substring(0, lastDot) : nameWithoutParams;
    }

    private static string GetTestName(string displayName, string fullyQualifiedName)
    {
        var className = GetClassName(fullyQualifiedName);

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (displayName.StartsWith(className + ".", StringComparison.InvariantCulture))
        {
            return displayName.Substring(className.Length + 1);
        }

        // For Theory tests, the display name might be "MethodName(param1, param2)"
        return displayName;
    }

    private static string? GetBaseMethodName(string testName)
    {
        var parenIndex = testName.IndexOf('(');
        return parenIndex > 0 ? testName.Substring(0, parenIndex) : null;
    }

}
