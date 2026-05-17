#!/usr/bin/env bash
set -e

# Define the source tag or branch
BRANCH="main"
BASE_URL="https://raw.githubusercontent.com/dotnet/iot/${BRANCH}/src/System.Device.Gpio/System/Device/Gpio"

FILES=(
  "GpioController.cs"
  "GpioDriver.cs"
  "GpioPin.cs"
  "PinChangeEventHandler.cs"
  "PinEventTypes.cs"
  "PinMode.cs"
  "PinValue.cs"
  "PinValueChangedEventArgs.cs"
  "PinValuePair.cs"
  "WaitForEventResult.cs"
)

# Output directory for the official source
TARGET_DIR="src/System.Device.Gpio"
TEMP_DIR=$(mktemp -d)

# Automatically clean up the temp directory on exit
trap 'rm -rf "$TEMP_DIR"' EXIT

echo "Downloading official source from branch/tag '${BRANCH}' to $TEMP_DIR..."

for FILE in "${FILES[@]}"; do
  curl -sSL "$BASE_URL/$FILE" -o "$TEMP_DIR/$FILE"
done

echo "==========================================================="
echo "Comparing downloaded files with local implementation..."
echo "==========================================================="

# Use diff to show differences. We append || true so the script doesn't abort on diff exit code 1
diff -u "$TARGET_DIR" "$TEMP_DIR" || true

if [ "$1" == "--update" ] || [ "$1" == "-u" ]; then
  echo "==========================================================="
  echo "Updating local files..."
  for FILE in "${FILES[@]}"; do
    if [ -f "$TARGET_DIR/$FILE" ]; then
      cp "$TEMP_DIR/$FILE" "$TARGET_DIR/$FILE"
      echo "  Updated: $FILE"
    fi
  done
  echo "Local files updated successfully."
else
  echo "==========================================================="
  echo "Sync check complete (dry-run)."
  echo "Temporary files cleaned up automatically."
  echo ""
  echo "To automatically apply the official updates to our tracked files, run:"
  echo "  ./scripts/sync-official-source.sh --update"
fi
