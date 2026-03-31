# Babelserver.DotNet.TestLogger

A test logger for `dotnet test` listing all tests run, one by one.

## Note

If you're using xUnit, don't use this package directly. Use
[Babelserver.DotNet.TestAdapter.xUnit](https://www.nuget.org/packages/Babelserver.DotNet.TestAdapter.xUnit) instead,
which includes this logger automatically plus an xUnit adapter that suppresses xUnit's console noise.

Install this package directly only if you're using NUnit or MSTest (not xUnit).

## Usage (NUnit/MSTest)

```xml
<PackageReference Include="Babelserver.DotNet.TestLogger" Version="3.0.0-preview" />
```

Then run:
```bash
dotnet test --logger list
```

## Output

Parameterized tests are grouped by default:
```
  ✅ MyTheoryTest (4 runs) (10ms)
```

If some runs fail:
```
  ❌ MyTheoryTest (2/4 runs failed) (10ms)
    ► MyTheoryTest(input: 2, expected: 3)
    Error: Assert.Equal() Failure...
```

To show every parameterized test individually instead:
```bash
dotnet test -- Babelserver.CollapseTheories=false
```
```
  ✅ MyTheoryTest(input: 1, expected: 2) (2ms)
  ✅ MyTheoryTest(input: 2, expected: 4) (1ms)
  ✅ MyTheoryTest(input: 3, expected: 6) (0ms)
```

## Verbosity

By default, all tests are listed individually. Use `Verbosity` to reduce output:

```bash
# Only show failed tests and summary (great for CI)
dotnet test --logger "list;Verbosity=minimal"

# Just a one-line pass/fail summary
dotnet test --logger "list;Verbosity=quiet"
```

`minimal` with failures:
```
────────────────────────────────────────────────────────────
 T E S T S
────────────────────────────────────────────────────────────
  ❌ DeleteUser_WhenNotFound_ThrowsException (3ms)
    Error: Expected exception but none was thrown
────────────────────────────────────────────────────────────
❌ Tests: 42, Passed: 41, Failed: 1, Skipped: 0
────────────────────────────────────────────────────────────
```

`minimal` when all pass, and `quiet` in both cases:
```
✅ Tests: 42, Passed: 42, Failed: 0, Skipped: 0
```

## Console Output Suppression

Direct console output from test code (e.g. ASP.NET host logging, Kafka clients) is suppressed by default.
The logger's own output (test results, summary) is unaffected.

To disable suppression:
```bash
dotnet test --logger "list;SuppressConsoleOutput=false"
```

## Test Output (ITestOutputHelper)

When tests fail, output written via `ITestOutputHelper` (xUnit) or equivalent is shown automatically:
```
  ❌ CreateUser_WhenDuplicate_Throws (12ms)
    Output:
      Debug: created user with id 42
      Debug: attempting to create duplicate...
    Error: Expected DuplicateException but got none
```

To also show output for passing tests (useful for debugging):
```bash
dotnet test --logger "list;ShowTestOutput=always"
```

To disable output completely:
```bash
dotnet test --logger "list;ShowTestOutput=never"
```

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Verbosity` | `normal` | Output level: `normal`, `minimal` (failures only), or `quiet` (summary only) |
| `SuppressConsoleOutput` | `true` | Suppress direct `Console.Write` output during test execution |
| `ShowTestOutput` | `onfailure` | Show `ITestOutputHelper` output: `onfailure`, `always`, or `never` |
| `MaxStackTraceLines` | `5` | Max stack trace lines per failure. `0` = hide, `-1` = unlimited |

Note: `CollapseTheories` and `ShowTestList` are only configurable when using the xUnit adapter, which sets them as test properties.

## CI Usage

This logger is designed for human-readable output. For machine-readable results (CI integrations,
dashboards, etc.), combine it with a structured logger:

```bash
dotnet test --logger list --logger trx
```

Both loggers run in parallel — you get clean terminal output and a machine-readable file for your CI system.

## Requirements

- .NET 8.0+ (including .NET 9.0) or .NET Framework 4.7.2+
