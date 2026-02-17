namespace Xunit.Runner.VisualStudio;

/// <summary>
/// Suppresses direct console output during test execution.
/// Redirects Console.Out and Console.Error to TextWriter.Null,
/// restoring the originals on Dispose.
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
