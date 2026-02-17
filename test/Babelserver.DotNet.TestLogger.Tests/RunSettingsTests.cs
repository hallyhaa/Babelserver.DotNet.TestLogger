using Xunit.Runner.VisualStudio;

namespace Babelserver.DotNet.TestLogger.Tests;

public class RunSettingsTests
{
    [Fact]
    public void Parse_SuppressConsoleOutput_True()
    {
        var xml = """
            <RunSettings>
              <Babelserver>
                <SuppressConsoleOutput>true</SuppressConsoleOutput>
              </Babelserver>
            </RunSettings>
            """;

        var settings = RunSettings.Parse(xml);

        Assert.True(settings.SuppressConsoleOutput);
    }

    [Fact]
    public void Parse_SuppressConsoleOutput_False()
    {
        var xml = """
            <RunSettings>
              <Babelserver>
                <SuppressConsoleOutput>false</SuppressConsoleOutput>
              </Babelserver>
            </RunSettings>
            """;

        var settings = RunSettings.Parse(xml);

        Assert.False(settings.SuppressConsoleOutput);
    }

    [Fact]
    public void Parse_SuppressConsoleOutput_Missing_DefaultsToNull()
    {
        var xml = "<RunSettings></RunSettings>";

        var settings = RunSettings.Parse(xml);

        Assert.Null(settings.SuppressConsoleOutput);
    }

    [Fact]
    public void Parse_CollapseTheories_True()
    {
        var xml = """
            <RunSettings>
              <Babelserver>
                <CollapseTheories>true</CollapseTheories>
              </Babelserver>
            </RunSettings>
            """;

        var settings = RunSettings.Parse(xml);

        Assert.True(settings.CollapseTheories);
    }

    [Fact]
    public void Parse_ShowTestList_False()
    {
        var xml = """
            <RunSettings>
              <Babelserver>
                <ShowTestList>false</ShowTestList>
              </Babelserver>
            </RunSettings>
            """;

        var settings = RunSettings.Parse(xml);

        Assert.False(settings.ShowTestList);
    }

    [Fact]
    public void Parse_MultipleBabelserverSettings()
    {
        var xml = """
            <RunSettings>
              <Babelserver>
                <CollapseTheories>false</CollapseTheories>
                <ShowTestList>true</ShowTestList>
                <SuppressConsoleOutput>false</SuppressConsoleOutput>
              </Babelserver>
            </RunSettings>
            """;

        var settings = RunSettings.Parse(xml);

        Assert.False(settings.CollapseTheories);
        Assert.True(settings.ShowTestList);
        Assert.False(settings.SuppressConsoleOutput);
    }

    [Fact]
    public void Parse_NullXml_ReturnsDefaults()
    {
        var settings = RunSettings.Parse(null);

        Assert.Null(settings.CollapseTheories);
        Assert.Null(settings.ShowTestList);
        Assert.Null(settings.SuppressConsoleOutput);
    }
}
