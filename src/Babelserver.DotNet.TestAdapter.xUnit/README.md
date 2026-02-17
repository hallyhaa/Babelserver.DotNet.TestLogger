# .NET Test Logger

A test logger for `dotnet test` that provides test output with status indicators for each individual test, inspired
by Maven's Surefire output and matching the [Gradle Test Logger Plugin](https://github.com/hallyhaa/gradle-test-logger).

## Features

- **Streaming output** — results print per class as each class completes, not at the end
- **Failing classes last** — classes with test failures are deferred to the end of output
- **Theory grouping** — parameterized tests grouped into a single line with live-updating run count
- Shows pass/fail/skip status for each individual test
- Displays a summary with test counts
- Skip reasons displayed when available
- Error messages and stack traces included for failed tests
- Suppresses all `[xUnit.net ...]` noise during execution

## Sample Output

```
────────────────────────────────────────────────────────────
 T E S T S
────────────────────────────────────────────────────────────
Running MyProject.Tests.UserServiceTests
  ✅ CreateUser_WithValidData_ReturnsUser (42ms)
  ✅ CreateUser_WithInvalidEmail_ThrowsException (8ms)
  ❌ DeleteUser_WhenNotFound_ThrowsException (3ms)
    Error: Expected exception but none was thrown
  ⏭️  UpdateUser_Integration (Requires database connection)
Running MyProject.Tests.OrderServiceTests
  ✅ PlaceOrder_WithStock_Succeeds (21ms)
────────────────────────────────────────────────────────────
❌ Tests: 5, Passed: 3, Failed: 1, Skipped: 1
────────────────────────────────────────────────────────────
```

On older Windows terminals without modern Unicode support:
```
------------------------------------------------------------
 T E S T S
------------------------------------------------------------
Running MyProject.Tests.UserServiceTests
  [PASS] CreateUser_WithValidData_ReturnsUser (42ms)
  [PASS] CreateUser_WithInvalidEmail_ThrowsException (8ms)
  [FAIL] DeleteUser_WhenNotFound_ThrowsException (3ms)
  [SKIP] UpdateUser_Integration (Requires database connection)
------------------------------------------------------------
```

## Installation

Add to your test project's `.csproj`:

```xml
<PackageReference Include="Babelserver.DotNet.TestAdapter.xUnit" Version="3.0.0-preview" />
```

This single package includes both an xUnit adapter that suppresses xUnit's console noise and the
Babelserver.DotNet.TestLogger implementation of Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.ITestLogger.

**Important:** Do remove `xunit.runner.visualstudio` from your project (this package replaces it). If both are present,
tests may run twice and xUnit's console noise will reappear.

## Theory Grouping

Parameterized tests are grouped into a single line by default:
```
  ✅ MyTheoryTest (4 runs) (10ms)
```

If some runs fail, details are shown:
```
  ❌ MyTheoryTest (2/4 runs failed) (10ms)
    ► MyTheoryTest(input: 2, expected: 3)
    Error: Assert.Equal() Failure...
```

To disable grouping and show each parameterized run individually:
```bash
dotnet test -- Babelserver.CollapseTheories=false
```

## Configuration

All standard [xUnit configuration options](https://xunit.net/docs/configuration-files) are supported via CLI:

```bash
dotnet test -- xUnit.ParallelizeTestCollections=false
dotnet test -- xUnit.StopOnFail=true
```

In addition, this package adds:

| Setting | Default | Description |
|---------|---------|-------------|
| `Babelserver.CollapseTheories` | `true` | Group Theory/MemberData runs into a single line |
| `Babelserver.ShowTestList` | `true` | Show per-test output (set `false` to behave like standard xUnit) |
| `Babelserver.SuppressConsoleOutput` | `true` | Suppress direct `Console.Write` output during test execution (e.g. ASP.NET host logging) |

## Requirements

- .NET 8.0+ (including .NET 9.0) or .NET Framework 4.7.2+
- xUnit v3 (3.1.0+)

## Related Packages

| Package | Description |
|---------|-------------|
| [Babelserver.DotNet.TestLogger](https://www.nuget.org/packages/Babelserver.DotNet.TestLogger) | Standalone logger for NUnit/MSTest (included automatically in this package) |


## How It Works

This package set out with an ambition that turned out to be most easily fulfilled by forking
[xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) and making one, single modification: console
output suppression. This means you get **full compatibility** with the official xUnit adapter, including:

- Test filtering (`--filter`)
- Runsettings support
- Parallel execution
- Traits
- All other xUnit features

For documentation on these features, see the official [xUnit documentation](https://xunit.net/docs/configuration-files).
We have used v3.1.5 of xunit.runner.visualstudio as the base for our fork.

The differences from xunit.runner.visualstudio are:

- Direct console output (e.g. ASP.NET host logging, Kafka) is suppressed during execution (configurable)
- xUnit's reporter noise (`Finished:` messages etc.) is suppressed
- Test results stream per class as each class completes, rather than all at once at the end
- Classes with failures are shown last, so passing output isn't interrupted by error details
- The clean formatted output comes from our TestLogger
