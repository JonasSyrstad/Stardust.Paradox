@echo off
REM Build script for Gremlin Studio Installer
REM Prerequisites: .NET 8 SDK, Inno Setup 6 (iscc.exe in PATH or at default location)

echo ========================================
echo Building Stardust Gremlin Studio
echo ========================================

REM Navigate to the project directory
cd /d "%~dp0..\Stardust.Paradox.GremlinStudio"

REM Clean previous builds
echo.
echo Cleaning previous builds...
dotnet clean -c Release

REM Restore packages
echo.
echo Restoring packages...
dotnet restore

REM Publish the application
echo.
echo Publishing application (self-contained, single-file)...
dotnet publish -c Release -p:PublishProfile=win-x64

if errorlevel 1 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo Build completed successfully!
echo Output: bin\publish\win-x64\GremlinStudio.exe

REM Check for Inno Setup
echo.
echo ========================================
echo Creating Installer
echo ========================================

set ISCC_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set ISCC_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
) else (
    where iscc.exe >nul 2>&1
    if errorlevel 1 (
        echo.
        echo WARNING: Inno Setup not found!
        echo Please install Inno Setup 6 from: https://jrsoftware.org/isdl.php
        echo Or ensure iscc.exe is in your PATH.
        echo.
        echo The application has been built successfully.
        echo You can find the executable at: bin\publish\win-x64\GremlinStudio.exe
        pause
        exit /b 0
    )
    set ISCC_PATH=iscc.exe
)

REM Create dist directory
cd /d "%~dp0"
if not exist "..\..\dist" mkdir "..\..\dist"

REM Build the installer
echo.
echo Building installer with Inno Setup...
%ISCC_PATH% GremlinStudio.iss

if errorlevel 1 (
    echo.
    echo ERROR: Installer creation failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Installer created successfully!
echo Output: dist\GremlinStudio-1.0.0-Setup.exe
echo ========================================
pause
