# .NET Test Logger

A test logger for `dotnet test` that provides test output with status indicators for each individual test, inspired
by Maven's Surefire output and matching the [Gradle Test Logger Plugin](https://github.com/hallyhaa/gradle-test-logger).

## Features

- Shows pass/fail/skip status for each individual test
- Displays a summary with test counts
- Skip reasons displayed when available
- Error messages and stack traces included for failed tests

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
<PackageReference Include="Babelserver.DotNet.TestAdapter.xUnit" Version="2.0.0" />
```

This single package includes both an xUnit adapter that suppresses xUnit's console noise and the
Babelserver.DotNet.TestLogger implementation of Microsoft.VisualStudio.TestPlatform.ObjectModel.Client.ITestLogger.

**Important:** Do remove `xunit.runner.visualstudio` from your project (this package replaces it). If both are present,
tests may run twice and xUnit's console noise will reappear.

## Loggers

Two loggers are available:

| Logger | Command | Description |
|--------|---------|-------------|
| `list` | `dotnet test` | Groups parameterized tests (Theory/InlineData) into a single line |
| `listAll` | `dotnet test --logger listAll` | Shows every test run individually |

### Grouped output (default)

Parameterized tests are grouped into a single line:
```
  ✅ MyTheoryTest (4 runs) (10ms)
```

If some runs fail, details are shown:
```
  ❌ MyTheoryTest (2/4 runs failed) (10ms)
    ► MyTheoryTest(input: 2, expected: 3)
    Error: Assert.Equal() Failure...
```

### Verbose output

Use `--logger listAll` to see every parameterized test individually:
```
  ✅ MyTheoryTest(input: 1, expected: 2) (2ms)
  ✅ MyTheoryTest(input: 2, expected: 4) (1ms)
  ✅ MyTheoryTest(input: 3, expected: 6) (0ms)
```


## Requirements

- .NET 8.0 or later
- xUnit 2.x (for the xUnit adapter)

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
We have used v2.8.2 of xunit.runner.visualstudio as the base for our fork.

The only difference from xunit.runner.visualstudio is that xUnit's console noise is suppressed, replaced by the clean
formatted output from our TestLogger.
