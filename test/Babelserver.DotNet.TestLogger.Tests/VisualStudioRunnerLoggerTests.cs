using System.Text;
using Xunit.Runner.VisualStudio;

namespace Babelserver.DotNet.TestLogger.Tests;

public class VisualStudioRunnerLoggerTests
{
    /// <summary>
    /// Verifies that the xUnit runner code (merged into our adapter DLL) still
    /// contains the "Finished:" string that we filter out in VisualStudioRunnerLogger.
    /// If this test fails after an xUnit version upgrade, the suppression may need updating.
    /// </summary>
    [Fact]
    public void AdapterDll_StillContains_FinishedString()
    {
        var assembly = typeof(VsTestRunner).Assembly;
        var bytes = File.ReadAllBytes(assembly.Location);
        var content = Encoding.UTF8.GetString(bytes);

        Assert.Contains("Finished:", content);
    }
}
