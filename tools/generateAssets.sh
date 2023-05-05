#!/bin/bash
set -e

# Deps
sudo xbps-install -Sy ImageMagick > /dev/null

MODIFIED_FILES="$(git diff --name-only -r HEAD^1 HEAD)"
for file in $MODIFIED_FILES; do
  if [ "$file" == "res/debug/icon.svg" ] || [ "$file" == "res/release/icon.svg" ]; then
    echo "Generating assets for $file"

    # Remove previous assets
    cd "$(dirname "$file")"
    rm -f "${file%.*}.png" "${file%.*}.ico"

    # Convert to PNG
    convert "$file" -size 1024x1024 "${file%.*}.png"

    # Convert to ICO
    # https://stackoverflow.com/a/15104985
    convert "$file" -bordercolor white -border 0 \
      \( -clone 0 -resize 16x16 \) \
      \( -clone 0 -resize 32x32 \) \
      \( -clone 0 -resize 48x48 \) \
      \( -clone 0 -resize 64x64 \) \
      -delete 0 -alpha off -colors 256 "${file%.*}.ico"

    echo "Done converting $file"
  fi
done
