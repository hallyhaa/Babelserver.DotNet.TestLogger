# Babelserver.DotNet.TestLogger

A test logger for `dotnet test` listing all tests run, one by one.

## Note

If you're using xUnit, don't use this package directly. It is typically installed automatically via
Babelserver.DotNet.TestAdapter.xUnit, which includes an xUnit adapter.

Install this package directly only if you're using NUnit or MSTest (not xUnit).

## Usage (NUnit/MSTest)

```xml
<PackageReference Include="Babelserver.DotNet.TestLogger" Version="1.0.0" />
```

Then run:
```bash
dotnet test --logger list
```
