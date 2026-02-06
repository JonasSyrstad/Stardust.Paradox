# Gremlin Studio Installer

This folder contains the installer build scripts for Stardust Gremlin Studio.

## Prerequisites

1. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Inno Setup 6** - [Download](https://jrsoftware.org/isdl.php)

## Building the Installer

### Using the Build Script (Recommended)

Simply run the build script:

```cmd
build-installer.cmd
```

This will:
1. Clean and restore the project
2. Publish a self-contained single-file executable
3. Create the Windows installer using Inno Setup

### Manual Build Steps

1. **Publish the application:**
   ```cmd
   cd ..\Stardust.Paradox.GremlinStudio
   dotnet publish -c Release -p:PublishProfile=win-x64
   ```

2. **Create the installer:**
   ```cmd
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" GremlinStudio.iss
   ```

## Output

- **Executable**: `Stardust.Paradox.GremlinStudio\bin\publish\win-x64\GremlinStudio.exe`
- **Installer**: `dist\GremlinStudio-1.0.0-Setup.exe`

## Installer Features

- Modern Windows installer appearance
- Optional desktop shortcut
- Start Menu integration
- Clean uninstall support
- No admin privileges required (installs to user profile by default)

## Updating Version

To update the version number:

1. Edit `GremlinStudio.iss` and update `#define MyAppVersion`
2. Edit `Stardust.Paradox.GremlinStudio.csproj` and update `<Version>`
