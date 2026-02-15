namespace Babelserver.DotNet.TestLogger;

internal static class OutputStyle
{
#if NETFRAMEWORK
    // .NET Framework runs only on Windows
    private static readonly Lazy<bool> LazyUseAscii = new(() =>
    {
        var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        return string.IsNullOrEmpty(wtSession) && string.IsNullOrEmpty(termProgram);
    });
#else
    // Fallback on Windows w/o modern terminal (matches Gradle plugin logic)
    private static readonly Lazy<bool> LazyUseAscii = new(() =>
    {
        if (!OperatingSystem.IsWindows()) return false;

        // Check for modern terminal support
        var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");

        return string.IsNullOrEmpty(wtSession) && string.IsNullOrEmpty(termProgram);
    });
#endif

    private static bool UseAscii => LazyUseAscii.Value;

    // ANSI codes - OK in cmd.exe since Win10, apparently
    public const string Reset = "\e[0m";
    public const string Bold = "\e[1m";
    public const string Dim = "\e[2m";
    public const string Red = "\e[31m";
    public const string Green = "\e[32m";
    private const string Yellow = "\e[33m";
    public const string Cyan = "\e[36m";

    // Symbols with ASCII fallback
    public static string Passed => UseAscii ? "[PASS]" : "\u2705";   // ✅
    public static string Failed => UseAscii ? "[FAIL]" : "\u274c";   // ❌
    private static string Skipped => UseAscii ? "[SKIP]" : "\u23ed\ufe0f"; // ⏭️
    private static string Unknown => UseAscii ? "[????]" : "\u2753";  // ❓

    public static string HorizontalLine => new(UseAscii ? '-' : '\u2500', 60);

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds >= 1 ?
            $"{Dim}({duration.TotalSeconds:F1}s){Reset}" :
            $"{Dim}({duration.TotalMilliseconds:F0}ms){Reset}";

    public static string PassedResult(string testName, TimeSpan duration) =>
        $"  {Passed} {Green}{testName}{Reset} {FormatDuration(duration)}";

    public static string FailedResult(string testName, TimeSpan duration) =>
        $"  {Failed} {Red}{testName}{Reset} {FormatDuration(duration)}";

    public static string SkippedResult(string testName, string? reason = null)
    {
        var reasonText = string.IsNullOrEmpty(reason) ? "" : $" {Dim}({reason}){Reset}";
        // Extra space after ⏭️ in emoji mode to compensate for variation selector width
        var spacing = UseAscii ? " " : "  ";
        return $"  {Skipped}{spacing}{Yellow}{testName}{Reset}{reasonText}";
    }

    public static string UnknownResult(string testName, string outcome) =>
        $"  {Unknown} {Yellow}{testName}{Reset} {Dim}({outcome}){Reset}";

    public static string GroupedPassedResult(string testName, int runCount, TimeSpan duration) =>
        $"  {Passed} {Green}{testName}{Reset} {Dim}({runCount} {(runCount == 1 ? "run" : "runs")}){Reset} {FormatDuration(duration)}";

    public static string GroupedFailedResult(string testName, int failedCount, int totalCount, TimeSpan duration) =>
        $"  {Failed} {Red}{testName}{Reset} {Dim}({failedCount}/{totalCount} runs failed){Reset} {FormatDuration(duration)}";

    public static string GroupedSkippedResult(string testName, int skippedCount, int totalCount)
    {
        var spacing = UseAscii ? " " : "  ";
        return $"  {Skipped}{spacing}{Yellow}{testName}{Reset} {Dim}({skippedCount}/{totalCount} runs skipped){Reset}";
    }
}
