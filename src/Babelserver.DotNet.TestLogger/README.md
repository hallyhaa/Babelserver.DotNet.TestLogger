# Babelserver.DotNet.TestLogger

A test logger for `dotnet test` listing all tests run, one by one.

## Note

If you're using xUnit, don't use this package directly. It is typically installed automatically via
Babelserver.DotNet.TestAdapter.xUnit, which includes an xUnit adapter.

Install this package directly only if you're using NUnit or MSTest (not xUnit).

## Usage (NUnit/MSTest)

```xml
<PackageReference Include="Babelserver.DotNet.TestLogger" Version="1.0.1" />
```

Then run:
```bash
dotnet test --logger list
```

## Loggers

Two loggers are available:

| Logger | Description |
|--------|-------------|
| `list` | Groups parameterized tests (Theory/InlineData) into a single line |
| `listAll` | Shows every test run individually |

### Grouped output (default)

```bash
dotnet test --logger list
```

Parameterized tests are grouped:
```
  ✅ MyTheoryTest (4 runs) (10ms)
```

If some runs fail:
```
  ❌ MyTheoryTest (2/4 runs failed) (10ms)
    ► MyTheoryTest(input: 2, expected: 3)
    Error: Assert.Equal() Failure...
```

### Verbose output

```bash
dotnet test --logger listAll
```

Every parameterized test is shown individually:
```
  ✅ MyTheoryTest(input: 1, expected: 2) (2ms)
  ✅ MyTheoryTest(input: 2, expected: 4) (1ms)
  ✅ MyTheoryTest(input: 3, expected: 6) (0ms)
```
