using Xunit.Runner.VisualStudio;

namespace Babelserver.DotNet.TestLogger.Tests;

public class ConsoleSuppressorTests
{
    [Fact]
    public void Suppresses_ConsoleOut_During_Scope()
    {
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            using (new ConsoleSuppressor())
            {
                Console.WriteLine("should be suppressed");
            }

            Assert.Equal("", output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Restores_ConsoleOut_After_Dispose()
    {
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            using (new ConsoleSuppressor()) { }

            Console.WriteLine("after dispose");

            Assert.Contains("after dispose", output.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Suppresses_ConsoleError_During_Scope()
    {
        var originalError = Console.Error;
        var output = new StringWriter();
        Console.SetError(output);

        try
        {
            using (new ConsoleSuppressor())
            {
                Console.Error.WriteLine("should be suppressed");
            }

            Assert.Equal("", output.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
