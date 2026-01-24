# Babelserver.DotNet.TestLogger

A test logger for `dotnet test` with very simple output listing all performed tests, inspired by Maven's Surefire output
and matching the [Gradle Test Logger Plugin](https://github.com/hallyhaa/gradle-test-logger).

## Packages

| Package | Description                                                |
|---------|------------------------------------------------------------|
| [Babelserver.DotNet.TestAdapter.xUnit](src/Babelserver.DotNet.TestAdapter.xUnit/) | xUnit adapter using only the Babelserver.DotNet.TestLogger |
| [Babelserver.DotNet.TestLogger](src/Babelserver.DotNet.TestLogger/) | Standalone logger for NUnit/MSTest                         |

## Quick Start (xUnit)

```xml
<PackageReference Include="Babelserver.DotNet.TestAdapter.xUnit" Version="1.0.0" />
```

See the [full documentation](src/Babelserver.DotNet.TestAdapter.xUnit/README.md) for features, sample output, and configuration options.

## License

MIT
