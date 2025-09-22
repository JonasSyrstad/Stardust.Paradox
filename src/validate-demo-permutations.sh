#!/bin/bash

# Demo App Validation Script
# Tests all protocol permutations automatically

echo "?? Demo App Protocol Validation Script"
echo "======================================"

# Function to test framework
test_framework() {
    local framework=$1
    echo ""
    echo "?? Testing $framework..."
    
    # Build for specific framework
    dotnet build Stardust.Paradox.Data.InMemory.Demo --framework $framework --verbosity minimal
    
    if [ $? -eq 0 ]; then
        echo "? Build successful for $framework"
        
        # Run automated tests
        echo "?? Running automated tests..."
        dotnet run --framework $framework --project Stardust.Paradox.Data.InMemory.Demo -- --test
        
        if [ $? -eq 0 ]; then
            echo "? All tests passed for $framework"
            return 0
        else
            echo "? Tests failed for $framework"
            return 1
        fi
    else
        echo "? Build failed for $framework"
        return 1
    fi
}

# Test .NET 8 (full support expected)
echo "?? Testing .NET 8 (Full Protocol Support Expected)"
test_framework "net8.0"
net8_result=$?

# Summary
echo ""
echo "?? VALIDATION SUMMARY"
echo "===================="

if [ $net8_result -eq 0 ]; then
    echo "? .NET 8: All protocols working"
else
    echo "? .NET 8: Issues detected"
fi

# Overall result
if [ $net8_result -eq 0 ]; then
    echo ""
    echo "?? VALIDATION SUCCESSFUL"
    echo "All demo app permutations are working correctly!"
    exit 0
else
    echo ""
    echo "?? VALIDATION FAILED"
    echo "Some issues were detected. Please check the output above."
    exit 1
fi