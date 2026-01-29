using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Babelserver.DotNet.TestAdapter.xUnit;

[FileExtension(".dll")]
[FileExtension(".exe")]
[DefaultExecutorUri(QuietTestExecutor.ExecutorUri)]
public class QuietTestDiscoverer : ITestDiscoverer
{
    public void DiscoverTests(
        IEnumerable<string> sources,
        IDiscoveryContext discoveryContext,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink)
    {
        foreach (var source in sources)
        {
            DiscoverTestsInAssembly(source, discoverySink);
        }
    }

    private static void DiscoverTestsInAssembly(string assemblyPath, ITestCaseDiscoverySink discoverySink)
    {
        try
        {
            var configuration = ConfigReader.Load(assemblyPath, null);

            using var controller = new XunitFrontController(
                AppDomainSupport.IfAvailable,
                assemblyPath,
                configFileName: null,
                shadowCopy: false);

            var sink = new DiscoverySink(discoverySink, assemblyPath);

            controller.Find(
                includeSourceInformation: true,
                messageSink: sink,
                discoveryOptions: TestFrameworkOptions.ForDiscovery(configuration));

            sink.Finished.WaitOne();
        }
        catch
        {
            // Assembly doesn't contain xUnit tests
        }
    }
}

internal class DiscoverySink(ITestCaseDiscoverySink discoverySink, string assemblyPath) : IMessageSink
{
    public ManualResetEvent Finished { get; } = new(false);

    public bool OnMessage(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestCaseDiscoveryMessage discoveryMessage:
                var testCase = CreateTestCase(discoveryMessage.TestCase);
                discoverySink.SendTestCase(testCase);
                break;

            case IDiscoveryCompleteMessage:
                Finished.Set();
                break;
        }

        return true;
    }

    private TestCase CreateTestCase(ITestCase xunitTestCase)
    {
        var fqn = $"{xunitTestCase.TestMethod.TestClass.Class.Name}.{xunitTestCase.TestMethod.Method.Name}";
        var testCase = new TestCase(
            fqn,
            new Uri(QuietTestExecutor.ExecutorUri),
            assemblyPath)
        {
            DisplayName = xunitTestCase.DisplayName,
            CodeFilePath = xunitTestCase.SourceInformation?.FileName,
            LineNumber = xunitTestCase.SourceInformation?.LineNumber ?? 0
        };

        // Store the xUnit test case ID for execution
        testCase.SetPropertyValue(QuietTestExecutor.TestCaseIdProperty, xunitTestCase.UniqueID);

        return testCase;
    }
}
