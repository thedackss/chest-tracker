#!/usr/bin/env bash
# Builds dist/ChestTracker.exe (Windows x64, self-contained single file). Works on Linux and Windows (Git Bash).
# Extra arguments go to dotnet publish, e.g. ./publish.sh -p:Version=0.2.0
set -euo pipefail
cd "$(dirname "$0")"
dotnet publish src/ChestTracker/ChestTracker.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist "$@"
echo "dist/ChestTracker.exe"
