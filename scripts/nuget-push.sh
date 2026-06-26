#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/nupkg"
NUGET_SOURCE="https://api.nuget.org/v3/index.json"

PROJECTS=(
  "src/AiAssertions.Core/AiAssertions.Core.csproj"
  "src/AiAssertions/AiAssertions.csproj"
  "src/AiAssertions.OpenAi/AiAssertions.OpenAi.csproj"
  "src/AiAssertions.OpenRouter/AiAssertions.OpenRouter.csproj"
  "src/AiAssertions.DeepSeek/AiAssertions.DeepSeek.csproj"
  "src/AiAssertions.Anthropic/AiAssertions.Anthropic.csproj"
  "src/AiAssertions.Gemini/AiAssertions.Gemini.csproj"
  "src/AiAssertions.Grok/AiAssertions.Grok.csproj"
)

PROPS_VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT_DIR/Directory.Build.props" | head -n 1)"

if [ -n "$PROPS_VERSION" ]; then
  read -r -p "Found version $PROPS_VERSION in Directory.Build.props. Use it? [Y/n]: " USE_PROPS_VERSION
  if [[ "$USE_PROPS_VERSION" =~ ^[Nn]$ ]]; then
    read -r -p "Enter package version: " VERSION
  else
    VERSION="$PROPS_VERSION"
  fi
else
  read -r -p "Package version not found in Directory.Build.props. Enter package version: " VERSION
fi

if [ -z "$VERSION" ]; then
  echo "Package version cannot be empty"
  exit 1
fi

NUGET_API_KEY="${NUGET_API_KEY:-}"

if [ -z "$NUGET_API_KEY" ]; then
  read -r -s -p "NuGet API key: " NUGET_API_KEY
  echo
else
  echo "Using NuGet API key from NUGET_API_KEY."
fi

if [ -z "$NUGET_API_KEY" ]; then
  echo "NuGet API key cannot be empty"
  exit 1
fi

echo "Version: $VERSION"
echo "NuGet source: $NUGET_SOURCE"

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

dotnet clean "$ROOT_DIR/AiAssertions.slnx" -c Release
dotnet restore "$ROOT_DIR/AiAssertions.slnx"
dotnet build "$ROOT_DIR/AiAssertions.slnx" -c Release --no-restore

for project in "${PROJECTS[@]}"; do
  dotnet pack "$ROOT_DIR/$project" -c Release --no-build --output "$OUTPUT_DIR" /p:Version="$VERSION"
done

PACKAGES=(
  "$OUTPUT_DIR/AiAssertions.Core.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.Core.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.OpenAi.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.OpenAi.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.OpenRouter.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.OpenRouter.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.DeepSeek.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.DeepSeek.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.Anthropic.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.Anthropic.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.Gemini.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.Gemini.$VERSION.snupkg"
  "$OUTPUT_DIR/AiAssertions.Grok.$VERSION.nupkg"
  "$OUTPUT_DIR/AiAssertions.Grok.$VERSION.snupkg"
)

echo "Packages to push:"
for package in "${PACKAGES[@]}"; do
  if [ ! -f "$package" ]; then
    echo "Package not found: $package"
    exit 1
  fi

  echo "  ${package#$ROOT_DIR/}"
done

read -r -p "Continue? [Y/n]: " CONFIRM
if [[ "$CONFIRM" =~ ^[Nn]$ ]]; then
  echo "Cancelled."
  exit 0
fi

for package in "${PACKAGES[@]}"; do
  dotnet nuget push "$package" \
    --api-key "$NUGET_API_KEY" \
    --source "$NUGET_SOURCE" \
    --skip-duplicate
done

echo "Done!"
