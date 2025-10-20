# Installation

Plugin.ExceptionListeners provides two packages to cover different scenarios:

## Package Overview

### Core Package: Plugin.ExceptionListeners

The core package provides exception listening capabilities for standard .NET applications.

**Supports:**

- .NET 9.0 and later
- All platforms supported by .NET (Windows, Linux, macOS)
- Console applications, Web applications, Desktop applications

**Install via Package Manager:**

```bash
dotnet add package Plugin.ExceptionListeners
```

**Install via Package Manager UI:**

1. Right-click your project in Visual Studio
2. Select "Manage NuGet Packages"
3. Search for "Plugin.ExceptionListeners"
4. Click "Install"

### MAUI Package: Plugin.ExceptionListeners.Maui

The MAUI package extends the core functionality with native platform exception handling.

**Supports:**

- .NET 9.0 MAUI applications
- iOS, Android, Windows, macOS (via Mac Catalyst)
- Native exception interception and handling

**Install via Package Manager:**

```bash
dotnet add package Plugin.ExceptionListeners.Maui
```

> **Note:** The MAUI package automatically includes the core package as a dependency.

## Platform Requirements

### Minimum Requirements

| Platform | Version |
|----------|---------|
| .NET | 9.0 |
| iOS | 11.0+ |
| Android | API 21 (Android 5.0)+ |
| Windows | Windows 10 build 19041+ |
| macOS | 10.15+ (via Mac Catalyst) |

### Development Requirements

| Tool | Version |
|------|---------|
| Visual Studio | 2022 17.8+ |
| Visual Studio for Mac | 17.6+ |
| Visual Studio Code | Latest with C# extension |
| .NET SDK | 9.0+ |

## Installation Verification

After installing the packages, verify the installation by checking that you can import the namespaces:

### Core Package Verification

```csharp
using Plugin.ExceptionListeners;
using Plugin.ExceptionListeners.Listeners;

// Verify you can create listeners
var listener = new CurrentDomainFirstChanceExceptionListener(
    (sender, e) => Console.WriteLine($"Exception: {e.Exception.Message}")
);
```

### MAUI Package Verification

```csharp
using Plugin.ExceptionListeners.Maui;

// Verify you can create MAUI listeners
var nativeListener = new NativeUnhandledExceptionListener(
    (sender, e) => System.Diagnostics.Debug.WriteLine($"Native exception: {e.Exception.Message}")
);
```

## Troubleshooting Installation

### Common Issues

#### Package not found

- Ensure you're using the latest NuGet package source
- Try clearing your NuGet cache: `dotnet nuget locals all --clear`
- Verify your project targets a supported framework (.NET 9.0+)

#### MAUI package installation fails

- Ensure your project is configured for MAUI (`<UseMaui>true</UseMaui>`)
- Verify you have the MAUI workload installed: `dotnet workload list`
- Install MAUI workload if missing: `dotnet workload install maui`

#### Runtime errors on specific platforms

- Check that your target frameworks include the platforms you're deploying to
- Verify platform-specific dependencies are properly configured
- Ensure your app has appropriate permissions for exception handling

### Getting Help

If you encounter issues during installation:

1. Check the [GitHub Issues](https://github.com/framinosona/Plugin.ExceptionListeners/issues) page
2. Review the troubleshooting section in our documentation
3. Create a new issue with:
   - Your development environment details
   - Target framework and platform
   - Complete error messages
   - Minimal reproduction steps

## Next Steps

Once you've successfully installed the packages:

1. [Quick Start Guide](quick-start.md) - Get up and running in minutes
2. [Configuration Guide](configuration.md) - Learn about advanced configuration options
3. [Examples](../examples/basic-usage.md) - See practical implementation examples
