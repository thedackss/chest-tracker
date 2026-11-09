#!/usr/bin/env bash
# Builds dist/ChestTracker.exe (Windows x64, self-contained single file). Works on Linux and Windows (Git Bash).
set -euo pipefail
cd "$(dirname "$0")"
dotnet publish src/ChestTracker/ChestTracker.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist
echo "dist/ChestTracker.exe"
