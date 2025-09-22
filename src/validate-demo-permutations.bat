@echo off
REM Demo App Validation Script for Windows
REM Tests all protocol permutations automatically

echo ?? Demo App Protocol Validation Script
echo ======================================

echo.
echo ?? Testing .NET 8...

REM Build for .NET 8
dotnet build Stardust.Paradox.Data.InMemory.Demo --framework net8.0 --verbosity minimal

if %ERRORLEVEL% EQU 0 (
    echo ? Build successful for .NET 8
    
    echo ?? Running automated tests...
    dotnet run --framework net8.0 --project Stardust.Paradox.Data.InMemory.Demo -- --test
    
    if %ERRORLEVEL% EQU 0 (
        echo ? All tests passed for .NET 8
        set net8_result=0
    ) else (
        echo ? Tests failed for .NET 8
        set net8_result=1
    )
) else (
    echo ? Build failed for .NET 8
    set net8_result=1
)

echo.
echo ?? VALIDATION SUMMARY
echo ====================

if %net8_result% EQU 0 (
    echo ? .NET 8: All protocols working
) else (
    echo ? .NET 8: Issues detected
)

echo.
if %net8_result% EQU 0 (
    echo ?? VALIDATION SUCCESSFUL
    echo All demo app permutations are working correctly!
    exit /b 0
) else (
    echo ?? VALIDATION FAILED
    echo Some issues were detected. Please check the output above.
    exit /b 1
)