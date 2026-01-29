# Babelserver.DotNet.TestLogger

A test logger for `dotnet test` with very simple output listing all performed tests, inspired by Maven's Surefire output
and matching the [Gradle Test Logger Plugin](https://github.com/hallyhaa/gradle-test-logger).

## Packages

| Package | NuGet | Description |
|---------|-------|-------------|
| [Babelserver.DotNet.TestAdapter.xUnit](src/Babelserver.DotNet.TestAdapter.xUnit/) | [![NuGet](https://img.shields.io/nuget/v/Babelserver.DotNet.TestAdapter.xUnit)](https://www.nuget.org/packages/Babelserver.DotNet.TestAdapter.xUnit) | xUnit adapter (recommended) |
| [Babelserver.DotNet.TestLogger](src/Babelserver.DotNet.TestLogger/) | [![NuGet](https://img.shields.io/nuget/v/Babelserver.DotNet.TestLogger)](https://www.nuget.org/packages/Babelserver.DotNet.TestLogger) | Standalone logger for NUnit/MSTest |

## Quick Start (xUnit)

```xml
<PackageReference Include="Babelserver.DotNet.TestAdapter.xUnit" Version="1.0.2" />
```

See the [full documentation](src/Babelserver.DotNet.TestAdapter.xUnit/README.md) for features, sample output, and configuration options.

## License

MIT
