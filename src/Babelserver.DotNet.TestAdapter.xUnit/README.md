# .NET Test Logger

A test logger for `dotnet test` that provides test output with status indicators for each individual test, inspired
by Maven's Surefire output and matching the [Gradle Test Logger Plugin](https://github.com/hallyhaa/gradle-test-logger).

## Features

- Shows pass/fail/skip status for each individual test
- Displays a summary with test counts
- Skip reasons displayed when available
- Error messages and stack traces for failed tests

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
<PackageReference Include="Babelserver.DotNet.TestAdapter.xUnit" Version="1.0.1" />
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

## How It Works

This solution consists of two packages:

1. **Babelserver.DotNet.TestLogger** - An `ITestLoggerWithParameters` implementation that formats test results

2. **Babelserver.DotNet.TestAdapter.xUnit** - A minimal VSTest adapter that wraps xUnit execution while suppressing its
                                              console output. This package automatically includes the TestLogger.
