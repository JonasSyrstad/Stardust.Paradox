@echo off
REM Build and package Gremlin Studio release with Velopack
REM Run this script from the repository root

setlocal enabledelayedexpansion

set VERSION=1.2.0
set PROJECT=src\StardustGremlinStudio\Stardust.Paradox.GremlinStudio\Stardust.Paradox.GremlinStudio.csproj
set PUBLISH_DIR=src\StardustGremlinStudio\publish
set RELEASES_DIR=src\StardustGremlinStudio\releases

echo ========================================
echo Building Gremlin Studio v%VERSION%
echo ========================================

REM Clean previous builds
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%RELEASES_DIR%" rmdir /s /q "%RELEASES_DIR%"
mkdir "%RELEASES_DIR%"

REM Publish the application
echo.
echo Step 1: Publishing application...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained -o "%PUBLISH_DIR%"
if errorlevel 1 (
    echo ERROR: dotnet publish failed
    exit /b 1
)

REM Check if vpk is installed
where vpk >nul 2>nul
if errorlevel 1 (
    echo.
    echo Step 2: Installing Velopack CLI...
    dotnet tool install -g vpk
    if errorlevel 1 (
        echo ERROR: Failed to install Velopack CLI
        exit /b 1
    )
)

REM Package with Velopack
echo.
echo Step 3: Packaging with Velopack...
vpk pack ^
    --packId GremlinStudio ^
    --packVersion %VERSION% ^
    --packDir "%PUBLISH_DIR%" ^
    --mainExe GremlinStudio.exe ^
    --outputDir "%RELEASES_DIR%"

if errorlevel 1 (
    echo ERROR: Velopack packaging failed
    exit /b 1
)

echo.
echo ========================================
echo Build complete!
echo ========================================
echo.
echo Release files are in: %RELEASES_DIR%
echo.
echo Files to upload to GitHub release:
dir /b "%RELEASES_DIR%"
echo.
echo Next steps:
echo 1. Go to https://github.com/JonasSyrstad/Stardust.Paradox/releases/new
echo 2. Create a new release with tag "v%VERSION%"
echo 3. Upload all files from the releases folder
echo 4. Publish the release
echo.
pause
