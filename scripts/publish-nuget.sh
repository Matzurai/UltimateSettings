#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION_FILE="$REPOSITORY_ROOT/Version.txt"
ENV_FILE="$REPOSITORY_ROOT/.env.nuget"
PROJECT_FILE="$REPOSITORY_ROOT/UltimateSettings/UltimateSettings.csproj"
OUTPUT_DIRECTORY="$REPOSITORY_ROOT/artifacts/package"
PACKAGE_SOURCE="https://api.nuget.org/v3/index.json"

if [[ ! -f "$VERSION_FILE" ]]; then
    echo "Version file not found: $VERSION_FILE" >&2
    exit 1
fi

VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
    echo "Invalid package version in $VERSION_FILE: '$VERSION'" >&2
    exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
    echo "NuGet environment file not found: $ENV_FILE" >&2
    echo "Create it from .env.nuget.example and set NUGET_API_KEY." >&2
    exit 1
fi

# The local env file is intentionally ignored by git and must contain only local configuration.
set -a
# shellcheck disable=SC1090
source "$ENV_FILE"
set +a

if [[ -z "${NUGET_API_KEY:-}" || "$NUGET_API_KEY" == "replace-with-your-nuget-api-key" ]]; then
    echo "NUGET_API_KEY is missing or still contains the example value." >&2
    exit 1
fi

rm -rf "$OUTPUT_DIRECTORY"
mkdir -p "$OUTPUT_DIRECTORY"

echo "Packing UltimateSettings version $VERSION..."
dotnet pack "$PROJECT_FILE" \
    --configuration Release \
    --output "$OUTPUT_DIRECTORY" \
    -p:PackageVersion="$VERSION"

PACKAGE_FILE="$OUTPUT_DIRECTORY/UltimateSettings.$VERSION.nupkg"
SYMBOL_PACKAGE_FILE="$OUTPUT_DIRECTORY/UltimateSettings.$VERSION.snupkg"

if [[ ! -f "$PACKAGE_FILE" || ! -f "$SYMBOL_PACKAGE_FILE" ]]; then
    echo "Expected package files were not created." >&2
    exit 1
fi

echo
echo "Ready to publish:"
echo "  $PACKAGE_FILE"
echo "  $SYMBOL_PACKAGE_FILE"
read -r -p "Publish both packages to nuget.org? [y/N] " confirmation

if [[ ! "$confirmation" =~ ^[Yy]$ ]]; then
    echo "Publish cancelled. Packages remain in $OUTPUT_DIRECTORY."
    exit 0
fi

dotnet nuget push "$PACKAGE_FILE" \
    --api-key "$NUGET_API_KEY" \
    --source "$PACKAGE_SOURCE"

dotnet nuget push "$SYMBOL_PACKAGE_FILE" \
    --api-key "$NUGET_API_KEY" \
    --source "$PACKAGE_SOURCE"

echo "Published UltimateSettings $VERSION."
