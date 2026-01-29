namespace Xunit.Runner.VisualStudio;

/// <summary>
/// Suppresses console output to hide xUnit's noise.
/// This is the only intentional deviation from standard xunit.runner.visualstudio behavior.
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
