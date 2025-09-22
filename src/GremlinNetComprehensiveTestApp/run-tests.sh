#!/bin/bash
echo "========================================================================"
echo " GREMLIN.NET COMPREHENSIVE VALIDATION SUITE"
echo "========================================================================"
echo
echo "Running comprehensive tests for Gremlin.Net support and TinkerPop compliance..."
echo

cd "$(dirname "$0")"

echo "Building test application..."
dotnet build --configuration Release
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed!"
    exit 1
fi

echo
echo "Starting comprehensive test execution..."
echo "========================================================================"
echo

dotnet run --configuration Release

echo
echo "========================================================================"
echo "Test execution completed."
echo "========================================================================"
echo
echo "Key validation areas covered:"
echo "  - Basic server functionality"
echo "  - GraphSON version negotiation (v1, v2, v3)"
echo "  - Binary message handling"
echo "  - Protocol compatibility"
echo "  - Real Gremlin.Net client integration"
echo "  - TinkerPop protocol compliance"
echo "  - Wire protocol support (TCP, WebSocket, HTTP)"
echo "  - Comprehensive integration scenarios"
echo
echo "For detailed results, review the output above."
echo