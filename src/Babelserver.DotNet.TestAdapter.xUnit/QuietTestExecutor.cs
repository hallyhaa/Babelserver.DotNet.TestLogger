using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Xunit;
using Xunit.Abstractions;

namespace Babelserver.DotNet.TestAdapter.xUnit;

[ExtensionUri(ExecutorUri)]
public class QuietTestExecutor : ITestExecutor
{
    public const string ExecutorUri = "executor://Babelserver.DotNet.TestAdapter.xUnit/v1";

    public static readonly TestProperty TestCaseIdProperty = TestProperty.Register(
        "Babelserver.xUnit.TestCaseId",
        "xUnit Test Case ID",
        typeof(string),
        TestPropertyAttributes.Hidden,
        typeof(TestCase));

    private bool _cancelled;

    public void RunTests(IEnumerable<TestCase>? tests, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (tests == null || frameworkHandle == null)
            return;

        _cancelled = false;
        var testsByAssembly = tests.GroupBy(t => t.Source);

        foreach (var group in testsByAssembly)
        {
            if (_cancelled)
                break;

            RunTestsInAssembly(group.Key, group.ToList(), frameworkHandle);
        }
    }

    public void RunTests(IEnumerable<string>? sources, IRunContext? runContext, IFrameworkHandle? frameworkHandle)
    {
        if (sources == null || frameworkHandle == null)
            return;

        _cancelled = false;

        foreach (var source in sources)
        {
            if (_cancelled)
                break;

            RunAllTestsInAssembly(source, frameworkHandle);
        }
    }

    public void Cancel() => _cancelled = true;

    private static void RunAllTestsInAssembly(string assemblyPath, IFrameworkHandle frameworkHandle)
    {
        var configuration = ConfigReader.Load(assemblyPath, null);

        using var controller = new XunitFrontController(
            AppDomainSupport.IfAvailable,
            assemblyPath,
            configFileName: null,
            shadowCopy: false);

        var sink = new ExecutionSink(frameworkHandle, assemblyPath);

        // Suppress console output during execution
        using (new ConsoleSuppressor())
        {
            controller.RunAll(
                messageSink: sink,
                discoveryOptions: TestFrameworkOptions.ForDiscovery(configuration),
                executionOptions: TestFrameworkOptions.ForExecution(configuration));

            sink.Finished.WaitOne();
        }
    }

    private static void RunTestsInAssembly(string assemblyPath, List<TestCase> testCases, IFrameworkHandle frameworkHandle)
    {
        var configuration = ConfigReader.Load(assemblyPath, null);

        using var controller = new XunitFrontController(
            AppDomainSupport.IfAvailable,
            assemblyPath,
            configFileName: null,
            shadowCopy: false);

        // Get the xUnit test case IDs
        var testCaseIds = testCases
            .Select(tc => tc.GetPropertyValue<string>(TestCaseIdProperty, null))
            .Where(id => id != null)
            .Cast<string>()
            .ToList();

        var sink = new ExecutionSink(frameworkHandle, assemblyPath, testCases);

        // Find the actual xUnit test cases
        var discoverySink = new TestCaseCollector();

        using (new ConsoleSuppressor())
        {
            controller.Find(
                includeSourceInformation: false,
                messageSink: discoverySink,
                discoveryOptions: TestFrameworkOptions.ForDiscovery(configuration));
            discoverySink.Finished.WaitOne();

            var xunitTestCases = discoverySink.TestCases
                .Where(tc => testCaseIds.Contains(tc.UniqueID))
                .ToList();

            if (xunitTestCases.Count <= 0)
                return;

            controller.RunTests(
                xunitTestCases,
                messageSink: sink,
                executionOptions: TestFrameworkOptions.ForExecution(configuration));

            sink.Finished.WaitOne();
        }
    }
}

/// <summary>
/// Suppresses console output to hide xUnit's noise.
/// </summary>
internal class ConsoleSuppressor : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;

    public ConsoleSuppressor()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }
}

internal class TestCaseCollector : IMessageSink
{
    public List<ITestCase> TestCases { get; } = [];
    public ManualResetEvent Finished { get; } = new(false);

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestCaseDiscoveryMessage discoveryMessage:
                TestCases.Add(discoveryMessage.TestCase);
                break;

            case IDiscoveryCompleteMessage:
                Finished.Set();
                break;
        }

        return true;
    }
}

internal class ExecutionSink : IMessageSink
{
    private readonly IFrameworkHandle _frameworkHandle;
    private readonly string _assemblyPath;
    private readonly Dictionary<string, TestCase>? _testCaseLookup;

    public ManualResetEvent Finished { get; } = new(false);

    public ExecutionSink(IFrameworkHandle frameworkHandle, string assemblyPath, List<TestCase>? testCases = null)
    {
        _frameworkHandle = frameworkHandle;
        _assemblyPath = assemblyPath;

        if (testCases != null)
        {
            _testCaseLookup = testCases.ToDictionary(
                tc => tc.GetPropertyValue<string>(QuietTestExecutor.TestCaseIdProperty, ""),
                tc => tc);
        }
    }

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestPassed passed:
                RecordResult(passed.TestCase, TestOutcome.Passed,
                    TimeSpan.FromSeconds((double)passed.ExecutionTime));
                break;

            case ITestFailed failed:
                RecordResult(failed.TestCase, TestOutcome.Failed,
                    TimeSpan.FromSeconds((double)failed.ExecutionTime),
                    string.Join(Environment.NewLine, failed.Messages ?? []),
                    string.Join(Environment.NewLine, failed.StackTraces ?? []));
                break;

            case ITestSkipped skipped:
                RecordResult(skipped.TestCase, TestOutcome.Skipped, TimeSpan.Zero, skipped.Reason);
                break;

            case ITestAssemblyFinished:
                Finished.Set();
                break;
        }

        return true;
    }

    private void RecordResult(
        ITestCase xunitTestCase,
        TestOutcome outcome,
        TimeSpan duration,
        string? errorMessage = null,
        string? stackTrace = null)
    {
        if (_testCaseLookup != null && _testCaseLookup.TryGetValue(xunitTestCase.UniqueID, out var vsTestCase))
        {
            // Use existing test case
        }
        else
        {
            // Create a new test case for recording
            vsTestCase =
                new TestCase(xunitTestCase.DisplayName, new Uri(QuietTestExecutor.ExecutorUri), _assemblyPath)
            {
                DisplayName = xunitTestCase.DisplayName
            };
        }

        var result = new TestResult(vsTestCase)
        {
            Outcome = outcome,
            Duration = duration,
            ErrorMessage = errorMessage,
            ErrorStackTrace = stackTrace
        };

        _frameworkHandle.RecordResult(result);
    }
}
