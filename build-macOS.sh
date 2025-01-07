#!/bin/bash
# Simple MacOS script for building the library and uploading to NuGet.

# Checking OS. This build script is only intended for use with MacOS and Apple Passwords/Keychain
if [[ "$OSTYPE" != "darwin"* ]]; then
	echo "This build script only works on MacOS. Aborting."
	exit 1
fi

# Testing before building and uploading
if ! dotnet test; then
    echo "Tests failed! Aborting build and upload."
    exit 1
fi

# Getting Nuget API key
PW_NAME="nuget"
PW_ACCOUNT="gcntools"
if ! NUGET_API_KEY=$(security find-generic-password -w -s "$PW_NAME" -a "$PW_ACCOUNT"); then
	echo "Failed to retrieve password"
	exit 1
fi

# Setting up output directory
OUTPUT_DIR="release"
rm -rf "${OUTPUT_DIR}"
mkdir "${OUTPUT_DIR}"

# Build and pack
dotnet build --configuration Release
dotnet pack --configuration Release --output "${OUTPUT_DIR}"

NUGET_PACKAGE_PATH=$(find "${OUTPUT_DIR}" -name "*.nupkg")
if [[ -z "$NUGET_PACKAGE_NAME" ]]; then
    echo "Failed to find .nupkg file"
    exit 1
fi

# Pushing package
if dotnet nuget push "${NUGET_PACKAGE_PATH}" \
    --api-key "${NUGET_API_KEY}" \
    --source https://api.nuget.org/v3/index.json; then
    
    rm -r "${OUTPUT_DIR}"
else
    echo "Failed to push package."
    exit 1
fi