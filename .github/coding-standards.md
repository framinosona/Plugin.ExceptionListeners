# .NET Project Coding Standards - AI Agent Instructions

## Project Architecture Standards

### MSBuild Configuration Pattern

All projects follow a three-tier MSBuild configuration:

**Directory.Packages.props** (Root level):

- Central package management with `ManagePackageVersionsCentrally=true`
- All package versions defined centrally for consistency
- Separate dependency groups: Core, Test, Build & Analysis

**Directory.Build.props** (Read BEFORE .csproj):

- Host machine OS detection (Windows/macOS/Linux/Unix)
- CI/CD environment detection (GitHub Actions, GitLab CI, Azure DevOps)
- Global exclusions (Archive folders, Documentation folders)
- Auto-configuration for Release builds in CI

**Directory.Build.targets** (Read AFTER .csproj):

- Project metadata variables: `$(Project_Name)`, `$(Project_Description)`, `$(Project_Copyright)`, `$(Project_Tags)`, `$(Project_Url)`
- Versioning: `$(Version_Full)`, `$(Version_Assembly)` with fallback defaults
- NuGet packaging configuration with conditional generation
- Documentation generation settings (XML docs)
- SourceLink integration for debugging
- Strict analyzer settings: `TreatWarningsAsErrors=true`, `AnalysisMode=AllEnabledByDefault`

### Project Structure Variables

`.csproj` files use standardized variables instead of hardcoded values:

```xml
<PropertyGroup>
  <Project_Name>Your.Library.Name</Project_Name>
  <Project_Description>Library description</Project_Description>
  <Project_Copyright>Your Name</Project_Copyright>
  <Project_Tags>keyword1;keyword2;keyword3</Project_Tags>
  <Project_Url>https://github.com/username/repo</Project_Url>
</PropertyGroup>
```

## Testing Standards

### Framework and Structure

- **xUnit** as the testing framework
- **FluentAssertions** v7.2.0 (for licensing compliance - v8+ requires commercial license)
- Test project naming: `{ProjectName}.Tests`
- Test class naming: `{ClassName}Tests`
- Test method naming: `Method_Scenario_ExpectedResult`

### Test Organization Pattern

Mirror your main project file structure in tests with strict 1:1 mapping:

- Each main project file should have exactly one corresponding test file
- Use identical naming: `ClassName.cs` → `ClassNameTests.cs`
- Avoid bundling multiple source files into single test files
- Avoid `Coverage_*` or utility test files - integrate tests into appropriate category files
- Test file organization should exactly mirror source code organization for maintainability

### Test Patterns

```csharp
// Standard test structure
[Fact]
public void Method_ValidInput_ReturnsExpectedResult()
{
    // Arrange
    var input = CreateTestData();

    // Act
    var result = input.SomeMethod();

    // Assert
    result.Should().Be(expectedValue);
}

// Exception testing
[Fact]
public void Method_InvalidInput_ThrowsException()
{
    // Arrange
    var invalidInput = CreateInvalidData();

    // Act
    Action act = () => invalidInput.SomeMethod();

    // Assert
    act.Should().Throw<ArgumentException>();
}
```

### Coverage Requirements

- Minimum 80% code coverage enforced in CI/CD
- Coverage collection via Coverlet with Cobertura format
- Exclude test projects and third-party assemblies from coverage

## Build and CI/CD Standards

### Solution Structure

- Use `.slnx` format for solution files (newer XML-based format)
- Separate projects for library and tests
- Optional documentation project using DocFX

### Build Commands Pattern

```bash
# Standard workflow
dotnet restore {ProjectName}.slnx
dotnet build {ProjectName}.slnx -c Release

# Testing with coverage
dotnet test {ProjectName}.Tests/{ProjectName}.Tests.csproj \
  --logger "trx" \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura

# Package generation
dotnet build -p:GeneratePackageOnBuild=true -p:Version_Full=X.Y.Z
```

### Documentation Generation

- Use **DocFX** via dotnet tool manifest (`.config/dotnet-tools.json`)
- Generate API documentation from XML comments
- Deploy to GitHub Pages automatically

### Versioning Strategy

- Semantic versioning: `{major}.{minor}.{patch}`
- CI/CD auto-increments patch version based on Git tags
- Version variables passed via MSBuild properties
- Support for assembly version separate from package version

## Code Quality Standards

### Analyzer Configuration

- Enable all .NET analyzers: `AnalysisMode=AllEnabledByDefault`
- Treat warnings as errors: `TreatWarningsAsErrors=true`
- Use Microsoft.CodeAnalysis.NetAnalyzers package
- Include SourceLink for debugging support

### Language Features

- Target latest .NET version (currently .NET 9)
- Enable nullable reference types: `Nullable=enable`
- Use latest C# language version: `LangVersion=latest`
- Enable implicit usings: `ImplicitUsings=enable`

### Error Handling Philosophy

- Throw meaningful exceptions with detailed context
- Use `ArgumentNullException.ThrowIfNull()` for null checks
- Provide alternative non-throwing methods when appropriate
- Include relevant state information in exception messages

## Performance Considerations

### Modern .NET Patterns

- Prefer `ReadOnlySpan<T>` and `Memory<T>` for performance-critical operations
- Use `SequenceEqual()` for array/span comparisons
- Implement `IDisposable` and `IAsyncDisposable` where appropriate
- Leverage `ValueTask` for potentially synchronous async operations

### Resource Management

- Dispose resources properly using `using` statements
- Implement finalizers only when necessary
- Use object pooling for frequently allocated objects when beneficial

## NuGet Packaging Standards

### Package Metadata

- Include package icon, license, and README files
- Use meaningful package tags for discoverability
- Set appropriate package output paths
- Include symbol packages (`.snupkg`) for debugging

### Deployment Strategy

- Deploy to NuGet.org on main branch merges
- Automatic GitHub releases with generated notes
- GitHub Pages deployment for documentation
- Multi-environment support (staging/production)

## GitHub Actions Workflow Pattern

### Job Structure

1. **Version Calculation**: Auto-increment based on existing tags
2. **Build/Test/Package**: Restore, build, test with coverage, package
3. **Documentation**: Generate and deploy docs (main branch only)
4. **Deploy**: Publish to NuGet.org (main branch only)
5. **Release**: Create Git tags and GitHub releases

### Permissions Required

```yaml
permissions:
  contents: write      # For tags and releases
  packages: write      # For package publishing
  pages: write         # For GitHub Pages
  checks: write        # For test result publishing
  pull-requests: write # For PR comments
```

When creating new projects, copy this structure and adapt the project-specific variables while maintaining the overall architecture and quality standards.
